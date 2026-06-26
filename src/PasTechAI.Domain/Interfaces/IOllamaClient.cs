namespace PasTechAI.Domain.Interfaces;

public record OllamaMessage(string Role, string Content);

public record OllamaOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int NumCtx { get; init; } = 8192;
    public int NumPredict { get; init; } = -1;
}

public interface IOllamaClient
{
    IAsyncEnumerable<string> StreamChatAsync(
        string model,
        List<OllamaMessage> messages,
        OllamaOptions? options = null,
        CancellationToken ct = default);

    Task<string> GenerateAsync(
        string model,
        string prompt,
        OllamaOptions? options = null);

    Task<float[]> EmbedAsync(string text);
}
