using System.Text.Json;
using System.Text.RegularExpressions;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Application.Services;

public class MemoryService(IMemoryRepository repo, IOllamaClient ollama)
{
    private static readonly Regex JsonPattern = new(@"\{[\s\S]*?\}", RegexOptions.Compiled);

    public Task<List<UserMemory>> GetAsync(string userId) =>
        repo.GetByUserAsync(userId);

    public Task UpsertAsync(string userId, string type, string value) =>
        repo.UpsertAsync(userId, type, value);

    public async Task ExtractAndSaveAsync(string userId, List<ChatMessage> messages, string model = "qwen2.5:7b")
    {
        if (messages.Count == 0) return;

        var recent = messages.TakeLast(10)
            .Select(m => $"{(m.Role == "user" ? "User" : "AI")}: {m.Content[..Math.Min(m.Content.Length, 200)]}")
            .Aggregate((a, b) => $"{a}\n{b}");

        var prompt = $$"""
            วิเคราะห์บทสนทนาต่อไปนี้แล้วตอบเป็น JSON เท่านั้น (ไม่ต้องอธิบาย):
            {"Company":null,"Language":null,"Project":null,"Name":null,"Interest":null,"Role":null}
            ถ้าไม่พบข้อมูลให้ใส่ null

            บทสนทนา:
            {{recent}}
            """;

        try
        {
            var result = await ollama.GenerateAsync(model, prompt,
                new OllamaOptions { Temperature = 0.1f, NumPredict = 150 });

            var match = JsonPattern.Match(result);
            if (!match.Success) return;

            var extracted = JsonSerializer.Deserialize<Dictionary<string, string?>>(match.Value);
            if (extracted is null) return;

            foreach (var (key, val) in extracted)
                if (!string.IsNullOrWhiteSpace(val))
                    await repo.UpsertAsync(userId, key, val);
        }
        catch { /* ไม่ block main flow */ }
    }
}
