using Castle.DynamicProxy;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using GroundUp.infrastructure.services;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GroundUp.infrastructure.interceptors
{
    public class PermissionInterceptor : IInterceptor, IPermissionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermissionService _permissionService;

        public PermissionInterceptor(IHttpContextAccessor httpContextAccessor, IPermissionService permissionService)
        {
            _httpContextAccessor = httpContextAccessor;
            _permissionService = permissionService;
        }

        public void Intercept(IInvocation invocation)
        {
            var method = invocation.MethodInvocationTarget ?? invocation.Method;
            var permissionAttribute = method.GetCustomAttribute<RequiresPermissionAttribute>();

            if (permissionAttribute != null)
            {
                var userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var hasPermission = Task.Run(async () => await _permissionService.HasPermission(userId, permissionAttribute.Permission)).Result;

                if (!hasPermission)
                {
                    throw new UnauthorizedAccessException($"User '{userId}' lacks permission: {permissionAttribute.Permission}");
                }
            }

            invocation.Proceed();
        }
    }
}
