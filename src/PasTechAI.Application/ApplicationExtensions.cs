using Microsoft.Extensions.DependencyInjection;
using PasTechAI.Application.Services;

namespace PasTechAI.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<QueryRewriteService>();
        services.AddScoped<MemoryService>();
        services.AddScoped<SummaryService>();
        services.AddScoped<ChatOrchestrator>();
        return services;
    }
}
