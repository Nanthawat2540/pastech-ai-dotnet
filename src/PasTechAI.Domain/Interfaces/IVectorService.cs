namespace PasTechAI.Domain.Interfaces;

public record VectorPayload(
    string Type,
    string? UserId = null,
    string? SourceId = null,
    string TextPreview = "");

public record VectorResult(
    string Id,
    string Type,
    string TextPreview,
    float Score,
    string? UserId = null,
    string? SourceId = null);

public interface IVectorService
{
    Task<List<VectorResult>> SearchAsync(
        List<string> queries,
        string[]? types = null,
        string? userId = null,
        int topK = 5);

    Task IndexAsync(string id, string text, VectorPayload payload);
    Task DeleteAsync(string id);
    Task EnsureCollectionAsync();
}
