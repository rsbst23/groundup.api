using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Services.Core.Interceptors;

public sealed class LazyInterceptor : IInterceptor
{
    private readonly IServiceProvider _provider;
    private PermissionInterceptor? _interceptor;

    public LazyInterceptor(IServiceProvider provider)
    {
        _provider = provider;
    }

    public void Intercept(IInvocation invocation)
    {
        // Lazy-load `PermissionInterceptor` ONLY when a method is called
        _interceptor ??= _provider.GetRequiredService<PermissionInterceptor>();
        _interceptor.Intercept(invocation);
    }
}
