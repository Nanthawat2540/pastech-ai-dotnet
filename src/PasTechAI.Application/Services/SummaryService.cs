using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Application.Services;

public class SummaryService(ISummaryRepository repo, IChatRepository chatRepo, IOllamaClient ollama)
{
    public Task<List<ConversationSummary>> GetRecentAsync(string userId, int limit = 2) =>
        repo.GetRecentByUserAsync(userId, limit);

    public async Task GenerateAndSaveAsync(string sessionId, string userId)
    {
        var messages = await chatRepo.GetBySessionAsync(sessionId);
        if (messages.Count < 10) return;

        var transcript = messages.TakeLast(100)
            .Select(m => $"{(m.Role == "user" ? "User" : "AI")}: {m.Content[..Math.Min(m.Content.Length, 300)]}")
            .Aggregate((a, b) => $"{a}\n{b}");

        var prompt = $"""
            สรุปบทสนทนาต่อไปนี้เป็นภาษาไทย bullet points 3-5 ข้อ:
            - กำลังทำอะไร / โปรเจกต์อะไร
            - technology ที่ใช้
            - สถานะล่าสุด / ปัญหาที่พบ

            บทสนทนา:
            {transcript}
            """;

        try
        {
            var summary = await ollama.GenerateAsync("qwen2.5:7b", prompt,
                new OllamaOptions { Temperature = 0.2f, NumPredict = 400 });

            if (!string.IsNullOrWhiteSpace(summary))
                await repo.SaveAsync(sessionId, userId, summary);
        }
        catch { /* ไม่ block main flow */ }
    }
}
