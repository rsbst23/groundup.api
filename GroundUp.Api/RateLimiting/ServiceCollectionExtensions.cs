using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading.RateLimiting;

namespace GroundUp.Api.RateLimiting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Opt-in registration for GroundUp rate limiting policies.
    /// </summary>
    public static IServiceCollection AddGroundUpRateLimiting(this IServiceCollection services)
    {
        Action<RateLimiterOptions> configure = options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.AddPolicy("AdminApiPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        };

        InvokeAddRateLimiterIfAvailable(services, configure);
        return services;
    }

    private static void InvokeAddRateLimiterIfAvailable(IServiceCollection services, Action<RateLimiterOptions> configure)
    {
        var configureType = configure.GetType(); // Action<RateLimiterOptions>

        var method = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
            })
            .Where(t => t is { IsAbstract: true, IsSealed: true })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m =>
            {
                if (m.Name != "AddRateLimiter") return false;
                var p = m.GetParameters();
                return p.Length == 2 &&
                       typeof(IServiceCollection).IsAssignableFrom(p[0].ParameterType) &&
                       p[1].ParameterType == configureType;
            });

        if (method == null)
        {
            // If AddRateLimiter isn't present in this app, the host must not call UseRateLimiter().
            return;
        }

        method.Invoke(null, new object?[] { services, configure });
    }
}
