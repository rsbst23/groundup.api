using GroundUp.Api.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Api;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MVC controllers shipped by the GroundUp API controller library.
    /// Call this from a host app (e.g. GroundUp.Sample / FutureApp.Api).
    /// </summary>
    public static IMvcBuilder AddGroundUpApiControllers(this IServiceCollection services)
    {
        var mvc = services.AddControllers();
        mvc.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(AuthController).Assembly));
        return mvc;
    }
}
