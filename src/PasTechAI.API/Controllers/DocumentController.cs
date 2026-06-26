using Microsoft.AspNetCore.Mvc;
using PasTechAI.Application.Models;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController(IVectorService vector) : ControllerBase
{
    /// <summary>POST /api/document — index text chunk into Qdrant</summary>
    [HttpPost]
    public async Task<IActionResult> IndexAsync([FromBody] IndexDocumentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { error = "text is required" });

        // Split into chunks of ~500 chars with 50-char overlap
        var chunks = SplitText(req.Text, 500, 50);
        var indexed = 0;
        foreach (var chunk in chunks)
        {
            await vector.IndexAsync(
                Guid.NewGuid().ToString(),
                chunk,
                new VectorPayload(req.Type, req.UserId, req.SourceId));
            indexed++;
        }

        return Ok(new { chunks = indexed });
    }

    private static List<string> SplitText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var end = Math.Min(i + chunkSize, text.Length);
            chunks.Add(text[i..end]);
            i += chunkSize - overlap;
        }
        return chunks;
    }
}
