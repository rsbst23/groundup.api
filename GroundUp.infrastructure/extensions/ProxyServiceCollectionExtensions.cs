using Castle.DynamicProxy;
using GroundUp.infrastructure.interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.infrastructure.extensions;

internal static class ProxyServiceCollectionExtensions
{
    /// <summary>
    /// Registers an interface-based service as a Castle DynamicProxy proxy so that
    /// <see cref="PermissionInterceptor"/> can enforce <c>[RequiresPermission]</c>
    /// declared on the service interface.
    ///
    /// Important:
    /// - Proxy only services (not repositories)
    /// - Service implementation must be registered as a concrete type
    /// </summary>
    public static IServiceCollection AddProxiedScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddScoped<TImplementation>();

        services.AddScoped<TInterface>(sp =>
        {
            var generator = sp.GetRequiredService<ProxyGenerator>();
            var interceptor = sp.GetRequiredService<PermissionInterceptor>();
            var impl = sp.GetRequiredService<TImplementation>();

            return generator.CreateInterfaceProxyWithTargetInterface<TInterface>(impl, interceptor);
        });

        return services;
    }
}
