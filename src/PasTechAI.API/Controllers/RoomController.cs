using Microsoft.AspNetCore.Mvc;
using PasTechAI.Application.Models;
using PasTechAI.Application.Services;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController(RoomBookingOrchestrator orchestrator, IRoomRepository roomRepo) : ControllerBase
{
    /// <summary>GET /api/room — list all rooms</summary>
    [HttpGet]
    public async Task<IActionResult> GetRoomsAsync()
    {
        var rooms = await roomRepo.GetAllAsync();
        return Ok(rooms);
    }

    /// <summary>POST /api/room/chat — room booking chat with SSE streaming</summary>
    [HttpPost("chat")]
    public async Task ChatAsync([FromBody] ChatRequest req, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var chunk in orchestrator.ChatAsync(req, ct))
        {
            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(chunk)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
    }
}
