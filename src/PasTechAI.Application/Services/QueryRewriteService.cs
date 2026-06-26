using System.Text.Json;
using System.Text.RegularExpressions;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Application.Services;

public class QueryRewriteService(IOllamaClient ollama)
{
    private static readonly Regex ArrayPattern = new(@"\[[\s\S]*?\]", RegexOptions.Compiled);

    public async Task<List<string>> RewriteAsync(string query, string userId)
    {
        if (query.Length < 3) return [query];

        var prompt = $"""
            แปลงคำค้นต่อไปนี้เป็น JSON array ของคำค้นหลายรูปแบบ (3-5 รูปแบบ)
            รวมทั้ง: แก้คำสะกดผิด, ขยายคำย่อ, คำภาษาไทย+อังกฤษ, คำพ้องความหมาย
            ตอบเฉพาะ JSON array เท่านั้น เช่น ["คำ1","คำ2","คำ3"]

            คำค้น: {query}
            """;

        try
        {
            var result = await ollama.GenerateAsync("qwen2.5:7b", prompt,
                new OllamaOptions { Temperature = 0.1f, NumPredict = 150 });

            var match = ArrayPattern.Match(result);
            if (!match.Success) return [query];

            var variants = JsonSerializer.Deserialize<List<string>>(match.Value) ?? [];
            variants.Insert(0, query);
            return variants.Distinct().Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        }
        catch
        {
            return [query];
        }
    }
}
