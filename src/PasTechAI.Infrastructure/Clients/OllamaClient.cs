using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Clients;

public class OllamaClient(HttpClient http) : IOllamaClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<string> StreamChatAsync(
        string model,
        List<OllamaMessage> messages,
        OllamaOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var opts = options ?? new OllamaOptions();
        var body = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            options = new { temperature = opts.Temperature, num_ctx = opts.NumCtx, num_predict = opts.NumPredict }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(body)
        };
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            OllamaChatChunk? chunk;
            try { chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line, JsonOpts); }
            catch { continue; }
            if (chunk?.Message?.Content is { Length: > 0 } content)
                yield return content;
            if (chunk?.Done == true) break;
        }
    }

    public async Task<string> GenerateAsync(string model, string prompt, OllamaOptions? options = null)
    {
        var opts = options ?? new OllamaOptions();
        var body = new
        {
            model,
            prompt,
            stream = false,
            options = new { temperature = opts.Temperature, num_predict = opts.NumPredict < 0 ? 300 : opts.NumPredict }
        };
        var res = await http.PostAsJsonAsync("/api/generate", body);
        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<OllamaGenerateResponse>(JsonOpts);
        return data?.Response ?? "";
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var body = new { model = "nomic-embed-text", prompt = text[..Math.Min(text.Length, 2000)] };
        var res = await http.PostAsJsonAsync("/api/embeddings", body);
        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOpts);
        return data?.Embedding ?? [];
    }

    private record OllamaChatChunk(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message,
        [property: JsonPropertyName("done")] bool Done);

    private record OllamaChatMessage(
        [property: JsonPropertyName("content")] string? Content);

    private record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response);

    private record OllamaEmbedResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);
}
