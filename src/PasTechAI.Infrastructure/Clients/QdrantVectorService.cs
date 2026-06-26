using Qdrant.Client;
using Qdrant.Client.Grpc;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Clients;

public class QdrantVectorService(QdrantClient qdrant, IOllamaClient ollama) : IVectorService
{
    private const string Collection = "pastech_kb";
    private const uint VectorSize = 768;

    public async Task EnsureCollectionAsync()
    {
        var collections = await qdrant.ListCollectionsAsync();
        if (collections.Any(c => c == Collection)) return;

        await qdrant.CreateCollectionAsync(Collection,
            new VectorParams { Size = VectorSize, Distance = Distance.Cosine });

        await qdrant.CreatePayloadIndexAsync(Collection, "type", PayloadSchemaType.Keyword);
        await qdrant.CreatePayloadIndexAsync(Collection, "user_id", PayloadSchemaType.Keyword);
    }

    public async Task<List<VectorResult>> SearchAsync(
        List<string> queries,
        string[]? types = null,
        string? userId = null,
        int topK = 5)
    {
        // Multi-query parallel search
        var tasks = queries.Select(q => SearchSingleAsync(q, types, userId, topK));
        var results = await Task.WhenAll(tasks);

        // Deduplicate by id, keep highest score
        return results
            .SelectMany(r => r)
            .GroupBy(r => r.Id)
            .Select(g => g.MaxBy(r => r.Score)!)
            .OrderByDescending(r => r.Score)
            .Take(topK * 2)
            .ToList();
    }

    private async Task<List<VectorResult>> SearchSingleAsync(
        string query, string[]? types, string? userId, int topK)
    {
        var vector = await ollama.EmbedAsync(query);
        if (vector.Length == 0) return [];

        Filter? filter = null;
        var must = new List<Condition>();

        if (types is { Length: > 0 })
        {
            if (types.Length == 1)
                must.Add(Match("type", types[0]));
            else
            {
                var typeFilter = new Filter();
                foreach (var t in types) typeFilter.Should.Add(Match("type", t));
                must.Add(new Condition { Filter = typeFilter });
            }
        }

        if (!string.IsNullOrEmpty(userId))
            must.Add(Match("user_id", userId));

        if (must.Count > 0)
        {
            filter = new Filter();
            foreach (var c in must) filter.Must.Add(c);
        }

        var hits = await qdrant.SearchAsync(Collection,
            vector,
            filter: filter,
            limit: (ulong)topK,
            scoreThreshold: 0.4f,
            payloadSelector: true);

        return hits.Select(h => new VectorResult(
            Id: h.Id.Uuid ?? h.Id.Num.ToString(),
            Type: h.Payload.TryGetValue("type", out var t) ? t.StringValue : "",
            TextPreview: h.Payload.TryGetValue("text_preview", out var tp) ? tp.StringValue : "",
            Score: h.Score,
            UserId: h.Payload.TryGetValue("user_id", out var uid) ? uid.StringValue : null,
            SourceId: h.Payload.TryGetValue("source_id", out var sid) ? sid.StringValue : null
        )).ToList();
    }

    public async Task IndexAsync(string id, string text, VectorPayload payload)
    {
        var vector = await ollama.EmbedAsync(text);
        if (vector.Length == 0) return;

        var preview = text.Length > 300 ? text[..300] : text;
        var pointPayload = new Dictionary<string, Value>
        {
            ["type"]         = payload.Type,
            ["text_preview"] = preview
        };
        if (!string.IsNullOrEmpty(payload.UserId))
            pointPayload["user_id"] = payload.UserId;
        if (!string.IsNullOrEmpty(payload.SourceId))
            pointPayload["source_id"] = payload.SourceId;

        await qdrant.UpsertAsync(Collection, [
            new PointStruct
            {
                Id = new PointId { Uuid = id },
                Vectors = vector,
                Payload = { pointPayload }
            }
        ]);
    }

    public async Task DeleteAsync(string id)
    {
        await qdrant.DeleteAsync(Collection, new PointId { Uuid = id });
    }

    private static Condition Match(string key, string value) =>
        new() { Field = new FieldCondition { Key = key, Match = new Match { Text = value } } };

}
