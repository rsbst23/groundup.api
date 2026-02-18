using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GroundUp.Api.Logging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Opt-in logging wiring for hosts. This method is intentionally minimal for now.
    /// A future settings system can drive provider selection (Console, CloudWatch, etc.).
    /// </summary>
    public static IServiceCollection AddGroundUpLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Placeholder: today GroundUp uses Serilog's static Log.
        // This method exists as a single opt-in integration point for FutureApp-style hosts.
        // Later: build logger configuration here based on GroundUp settings.
        services.AddSingleton(Log.Logger);
        return services;
    }
}
