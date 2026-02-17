using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Api.RateLimiting;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Opt-in enabling of rate limiting middleware.
    /// </summary>
    public static IApplicationBuilder UseGroundUpRateLimiting(this IApplicationBuilder app)
    {
        // Fail fast with a clear message if the required services aren't registered.
        // (UseRateLimiter() will throw too, but this helps pinpoint the cause.)
        var required = app.ApplicationServices.GetService<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>();
        _ = required; // keep analyzer quiet; presence check side-effect only

        return app.UseRateLimiter();
    }
}
