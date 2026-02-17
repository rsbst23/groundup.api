using GroundUp.Data.Core.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Data.Core;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies Core EF migrations (if the provider is relational).
    /// Intended for dev/local environments.
    /// </summary>
    public static IApplicationBuilder MigrateCoreDatabase(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }

        return app;
    }
}
