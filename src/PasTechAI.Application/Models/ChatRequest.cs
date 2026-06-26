namespace PasTechAI.Application.Models;

public record ChatRequest(
    string SessionId,
    string UserId,
    string Message,
    string Model = "qwen2.5:7b",
    string SystemPrompt = "");

public record IndexDocumentRequest(
    string Text,
    string Type,
    string? UserId = null,
    string? SourceId = null);
