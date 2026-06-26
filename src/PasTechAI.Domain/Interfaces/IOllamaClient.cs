using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasTechAI.Domain.Interfaces;

public record OllamaMessage(string Role, string Content);

public record OllamaOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int NumCtx { get; init; } = 8192;
    public int NumPredict { get; init; } = -1;
}

// Tool definitions
public record OllamaTool(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OllamaToolFunction Function);

public record OllamaToolFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] object Parameters);

// Raw message for tool calling conversations
public class OllamaRawMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OllamaRawToolCall>? ToolCalls { get; set; }
}

public class OllamaRawToolCall
{
    [JsonPropertyName("function")]
    public OllamaRawToolCallFunction Function { get; set; } = new();
}

public class OllamaRawToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

// Tool response from non-streaming call
public record OllamaToolCallItem(string Name, JsonElement Arguments);
public record OllamaToolResponse(string? Content, List<OllamaToolCallItem>? ToolCalls);

public interface IOllamaClient
{
    IAsyncEnumerable<string> StreamChatAsync(
        string model,
        List<OllamaMessage> messages,
        OllamaOptions? options = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamChatRawAsync(
        string model,
        List<OllamaRawMessage> messages,
        OllamaOptions? options = null,
        CancellationToken ct = default);

    Task<OllamaToolResponse> ChatWithToolsAsync(
        string model,
        List<OllamaRawMessage> messages,
        List<OllamaTool> tools);

    Task<string> GenerateAsync(
        string model,
        string prompt,
        OllamaOptions? options = null);

    Task<float[]> EmbedAsync(string text);
}
