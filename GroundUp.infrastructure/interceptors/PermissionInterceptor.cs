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
            var methodName = method.Name;
            var targetType = invocation.TargetType;
            var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

            // Find the interface that declares the method
            var interfaceType = targetType.GetInterfaces()
                .FirstOrDefault(i => i.GetMethod(methodName, parameterTypes) != null);

            MethodInfo? interfaceMethod = null;
            if (interfaceType != null)
            {
                interfaceMethod = interfaceType.GetMethod(methodName, parameterTypes);
            }

            var permissionAttribute = interfaceMethod?.GetCustomAttribute<RequiresPermissionAttribute>();

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

            // Get user roles from claims
            var roles = httpContext.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

            // Check if any user role matches required roles
            bool isAuthorized = false;
            if (permissionAttribute.RequiredRoles != null && permissionAttribute.RequiredRoles.Length > 0)
            {
                if (roles.Any(r => permissionAttribute.RequiredRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
                {
                    isAuthorized = true;
                }
            }

            // If not authorized by role, check permissions
            if (!isAuthorized && permissionAttribute.Permissions.Length > 0)
            {
                try
                {
                    isAuthorized = Task.Run(async () =>
                        await _permissionService.HasAnyPermission(userId, permissionAttribute.Permissions)
                    ).Result;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Permission check exception: {ex.Message}", ex);
                    throw;
                }
            }

            if (!isAuthorized)
            {
                throw new ForbiddenAccessException($"User {userId} lacks permission for {targetType.Name}.{methodName}");
            }

            invocation.Proceed();
        }
    }
}