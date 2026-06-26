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
    IConfiguration config) : ControllerBase
{
    /// <summary>GET /api/erp/config — returns CentralAuth login URL for ERP mode</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var loginUrl  = config["CentralAuth:LoginUrl"] ?? "";
        var appId     = config["CentralAuth:AppId"]   ?? "pastech-ai";
        return Ok(new { loginUrl, appId });
    }

    /// <summary>POST /api/erp/chat — ERP chat, requires CentralAuth JWT (Bearer token)</summary>
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

        // Build ERP-aware system prompt
        var erpPrompt = $"""
            คุณคือ AI Assistant ระบบ ERP ของ PasTech ตอบภาษาไทยกระชับ ตรงประเด็น
            ผู้ใช้งาน: {user.DisplayName} (username: {user.Username}, role: {user.Role})
            ข้อมูลที่เข้าถึงได้: ข้อมูลตามสิทธิ์ของ role "{user.Role}"
            กฎ:
            - ระวังข้อมูลที่เป็นความลับ ไม่แสดงข้อมูลเกินสิทธิ์
            - ถ้าไม่มีข้อมูลในระบบ ให้แจ้งว่าต้องตรวจสอบผ่านระบบ ERP โดยตรง
            - บันทึกการเข้าถึงข้อมูลสำคัญ (audit trail)
            """;

        var erpReq = req with
        {
            UserId       = user.Username,
            SystemPrompt = erpPrompt
        };

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"]      = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var chunk in orchestrator.ChatAsync(erpReq, ct))
        {
            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(chunk)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
    }
}
