using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.infrastructure.interceptors
{
    public class LazyInterceptor : IInterceptor
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
            if (_interceptor == null)
            {
                _interceptor = _provider.GetRequiredService<PermissionInterceptor>();
            }

            // Delegate the call to the actual interceptor
            _interceptor.Intercept(invocation);
        }
    }
}
