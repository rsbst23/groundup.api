using Castle.DynamicProxy;
using GroundUp.core.interfaces;
using GroundUp.Services.Core.Interceptors;
using GroundUp.Services.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Services.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Ensure proxy infra exists before registering proxied services.
        services.AddInfrastructureServices();

        services.AddProxiedScoped<IAuthFlowService, AuthFlowService>();
        services.AddProxiedScoped<IAuthUrlBuilderService, AuthUrlBuilderService>();
        services.AddProxiedScoped<IAuthSessionService, AuthSessionService>();
        services.AddProxiedScoped<IJoinLinkService, JoinLinkService>();
        services.AddProxiedScoped<IEnterpriseSignupService, EnterpriseSignupService>();
        services.AddProxiedScoped<ITenantSsoSettingsService, TenantSsoSettingsService>();
        services.AddProxiedScoped<IPermissionAdminService, PermissionAdminService>();
        services.AddProxiedScoped<IRoleService, RoleService>();
        services.AddProxiedScoped<ITenantService, TenantService>();
        services.AddProxiedScoped<IInvitationService, InvitationService>();
        services.AddProxiedScoped<ITenantJoinLinkService, TenantJoinLinkService>();
        services.AddProxiedScoped<IUserRoleService, UserRoleService>();
        services.AddProxiedScoped<IUserService, UserService>();
        services.AddProxiedScoped<IErrorFeedbackService, ErrorFeedbackService>();
        services.AddProxiedScoped<IPolicyService, PolicyService>();
        services.AddProxiedScoped<IRolePolicyService, RolePolicyService>();

        return services;
    }

    private static IServiceCollection AddProxiedScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<TInterface>(sp =>
        {
            var generator = sp.GetRequiredService<ProxyGenerator>();
            var impl = sp.GetRequiredService<TImplementation>();
            var interceptor = sp.GetRequiredService<PermissionInterceptor>();
            return generator.CreateInterfaceProxyWithTarget<TInterface>(impl, interceptor);
        });

        return services;
    }
}
