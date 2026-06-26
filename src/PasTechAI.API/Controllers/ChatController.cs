using Microsoft.AspNetCore.Mvc;
using PasTechAI.Application.Models;
using PasTechAI.Application.Services;

namespace PasTechAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(ChatOrchestrator orchestrator) : ControllerBase
{
    /// <summary>POST /api/chat — SSE streaming response</summary>
    [HttpPost]
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
