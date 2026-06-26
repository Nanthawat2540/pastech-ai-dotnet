using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasTechAI.Domain.Interfaces;
using PasTechAI.Infrastructure.Clients;
using PasTechAI.Infrastructure.Data;
using PasTechAI.Infrastructure.Repositories;
using Qdrant.Client;

namespace PasTechAI.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var connStr = config.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionString 'SqlServer' not found");

        var ollamaUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var qdrantHost = config["Qdrant:Host"] ?? "localhost";
        var qdrantPort = int.Parse(config["Qdrant:Port"] ?? "6334");

        // SQL Repositories
        services.AddSingleton<IChatRepository>(_ => new ChatRepository(connStr));
        services.AddSingleton<IMemoryRepository>(_ => new MemoryRepository(connStr));
        services.AddSingleton<ISummaryRepository>(_ => new SummaryRepository(connStr));
        services.AddSingleton<IRoomRepository>(_ => new RoomRepository(connStr));

        // Ollama HTTP client
        services.AddHttpClient<IOllamaClient, OllamaClient>(c =>
        {
            c.BaseAddress = new Uri(ollamaUrl);
            c.Timeout = TimeSpan.FromMinutes(10);
        });

        // Qdrant
        services.AddSingleton(_ => new QdrantClient(qdrantHost, qdrantPort));
        services.AddSingleton<IVectorService, QdrantVectorService>();

        // DB initializer
        services.AddSingleton(_ => new DbInitializer(connStr));

        return services;
    }
}
