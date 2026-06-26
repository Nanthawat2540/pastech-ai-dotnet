using Microsoft.AspNetCore.Mvc;
using PasTechAI.Application.Services;

namespace PasTechAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController(MemoryService memoryService) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAsync(string userId) =>
        Ok(await memoryService.GetAsync(userId));

    [HttpPost("{userId}")]
    public async Task<IActionResult> UpsertAsync(
        string userId,
        [FromBody] MemoryUpsertRequest req)
    {
        await memoryService.UpsertAsync(userId, req.MemoryType, req.MemoryValue);
        return Ok(new { success = true });
    }
}

public record MemoryUpsertRequest(string MemoryType, string MemoryValue);
