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
                var httpContext = _httpContextAccessor.HttpContext;

                // Check authentication
                if (httpContext?.User.Identity?.IsAuthenticated != true)
                {
                    throw new UnauthorizedAccessException("User is not authenticated.");
                }

                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("Unable to identify user.");
                }

                // Authorization logic
                bool isAuthorized = false;

                // Check role-only scenario
                if (permissionAttribute.RequiredRoles != null)
                {
                    isAuthorized = permissionAttribute.RequiredRoles
                        .Any(role => httpContext.User.IsInRole(role));
                }

                // Check permission-only scenario
                if (!isAuthorized && permissionAttribute.Permissions != null)
                {
                    isAuthorized = Task.Run(async () =>
                        await _permissionService.HasAnyPermission(
                            userId,
                            permissionAttribute.Permissions
                        )).Result;
                }

                if (!isAuthorized)
                {
                    throw new UnauthorizedAccessException(
                        "Insufficient permissions or roles to access this method.");
                }
            }

            invocation.Proceed();
        }
    }
}