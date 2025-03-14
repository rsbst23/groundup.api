using Castle.DynamicProxy;
using GroundUp.core.interfaces;
using GroundUp.core.security;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Security.Claims;

namespace GroundUp.infrastructure.interceptors
{
    public class PermissionInterceptor : IInterceptor, IPermissionInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermissionService _permissionService;
        private readonly ILoggingService _logger;

        public PermissionInterceptor(
            IHttpContextAccessor httpContextAccessor,
            IPermissionService permissionService,
            ILoggingService logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _permissionService = permissionService;
            _logger = logger;
        }

        public void Intercept(IInvocation invocation)
        {
            var method = invocation.MethodInvocationTarget ?? invocation.Method;
            var methodName = $"{method.DeclaringType?.Name}.{method.Name}";
            var permissionAttribute = method.GetCustomAttribute<RequiresPermissionAttribute>();

            if (permissionAttribute == null)
            {
                invocation.Proceed();
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Log user roles
            var roles = httpContext.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

            // Check permissions
            bool isAuthorized = false;
            try
            {
                if (permissionAttribute.Permissions.Length > 0)
                {
                    isAuthorized = Task.Run(async () =>
                        await _permissionService.HasAnyPermission(userId, permissionAttribute.Permissions)
                    ).Result;
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            if (!isAuthorized)
            {
                throw new ForbiddenAccessException($"User {userId} lacks permission for {methodName}");
            }

            invocation.Proceed();
        }
    }
}