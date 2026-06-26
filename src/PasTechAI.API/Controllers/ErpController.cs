using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PasTechAI.Application.Models;
using PasTechAI.Application.Services;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErpController(
    ChatOrchestrator orchestrator,
    ICentralAuthService authSvc,
    IConfiguration config,
    IHttpClientFactory httpFactory) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>POST /api/erp/login — proxy login to erp-auth, returns JWT</summary>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] ErpLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "กรุณากรอก Email และ Password" });

        var erpUrl = config["CentralAuth:ErpAuthUrl"] ?? "http://erp-auth:3001";
        var http   = httpFactory.CreateClient("erp-auth");
        http.BaseAddress = new Uri(erpUrl);

        try
        {
            var res = await http.PostAsJsonAsync("/api/v1/auth/login",
                new { email = req.Email, password = req.Password });

            var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

            if (!res.IsSuccessStatusCode)
            {
                var msg = body.TryGetProperty("message", out var m) ? m.GetString() : "Invalid credentials";
                return Unauthorized(new { error = msg });
            }

            // Extract token from { success: true, data: { tokens: { accessToken } }, user: {...} }
            var token = body
                .GetProperty("data")
                .GetProperty("tokens")
                .GetProperty("accessToken")
                .GetString();

            var user = body.GetProperty("data").GetProperty("user");

            return Ok(new
            {
                token,
                user = new
                {
                    email       = user.TryGetProperty("email",        out var e) ? e.GetString() : req.Email,
                    displayName = user.TryGetProperty("display_name", out var d) ? d.GetString()
                                : user.TryGetProperty("first_name",   out var f) ? f.GetString()
                                : req.Email,
                    roles = user.TryGetProperty("role_codes", out var r) ? r : default
                }
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { error = "ไม่สามารถเชื่อมต่อ ERP Auth Service ได้" });
        }
    }

    /// <summary>POST /api/erp/chat — ERP chat, requires erp-auth JWT (Bearer token)</summary>
    [HttpPost("chat")]
    public async Task ChatAsync([FromBody] ChatRequest req, CancellationToken ct)
    {
        var token = Request.Headers.Authorization.FirstOrDefault()
                        ?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(token))
        {
            Response.StatusCode = 401;
            await Response.WriteAsJsonAsync(new { error = "ต้องเข้าสู่ระบบก่อนใช้ระบบ ERP" }, ct);
            return;
        }

        var user = authSvc.ValidateToken(token);
        if (user is null)
        {
            Response.StatusCode = 401;
            await Response.WriteAsJsonAsync(new { error = "Token ไม่ถูกต้องหรือหมดอายุ กรุณา login ใหม่" }, ct);
            return;
        }

        if (!authSvc.HasErpAccess(user))
        {
            Response.StatusCode = 403;
            await Response.WriteAsJsonAsync(new
            {
                error = $"ผู้ใช้ {user.DisplayName} (role: {user.Role}) ไม่มีสิทธิ์เข้าถึงระบบ ERP"
            }, ct);
            return;
        }

        var roleList = string.Join(", ", user.Roles);
        var erpPrompt = $"""
            คุณคือ AI Assistant ระบบ ERP ของ PasTech ตอบภาษาไทยกระชับ ตรงประเด็น
            ผู้ใช้งาน: {user.DisplayName} (email: {user.Email})
            สิทธิ์: {roleList}
            กฎ:
            - แสดงข้อมูลตามสิทธิ์ของ role เท่านั้น
            - ถ้าไม่แน่ใจเรื่องข้อมูล ให้แจ้งว่าต้องตรวจสอบผ่านระบบ ERP โดยตรง
            - ห้ามเปิดเผยข้อมูลที่เป็นความลับหรือข้อมูลส่วนบุคคลที่ไม่จำเป็น
            """;

        var erpReq = req with
        {
            UserId       = user.Email,
            SystemPrompt = erpPrompt
        };

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"]      = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var chunk in orchestrator.ChatAsync(erpReq, ct))
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
    }
}

public record ErpLoginRequest(string Email, string Password);
