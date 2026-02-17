using GroundUp.Data.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GroundUp.Data.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core bounded-context repository implementations.
    /// </summary>
    public static IServiceCollection AddCoreRepositories(this IServiceCollection services)
    {
        // NOTE (mid-Phase 5): repos are being migrated from `GroundUp.infrastructure`.

        services.AddScoped<ITenantRepository, Repositories.TenantRepository>();
        services.AddScoped<IUserRepository, Repositories.UserRepository>();
        services.AddScoped<IUserTenantRepository, Repositories.UserTenantRepository>();
        services.AddScoped<IUserRoleRepository, Repositories.UserRoleRepository>();
        services.AddScoped<ITenantInvitationRepository, Repositories.TenantInvitationRepository>();
        services.AddScoped<ITenantJoinLinkRepository, Repositories.TenantJoinLinkRepository>();

        services.AddScoped<IPermissionRepository, Repositories.PermissionRepository>();
        services.AddScoped<IPermissionQueryRepository, Repositories.PermissionQueryRepository>();
        services.AddScoped<IRoleRepository, Repositories.RoleRepository>();
        services.AddScoped<IPolicyRepository, Repositories.PolicyRepository>();
        services.AddScoped<IRolePolicyRepository, Repositories.RolePolicyRepository>();

        services.AddScoped<ISystemRoleRepository, Repositories.SystemRoleRepository>();
        services.AddScoped<IErrorFeedbackRepository, Repositories.ErrorFeedbackRepository>();

        services.AddScoped<ITenantSsoSettingsRepository, Repositories.TenantSsoSettingsRepository>();
        services.AddScoped<IEnterpriseTenantProvisioningRepository, Repositories.EnterpriseTenantProvisioningRepository>();

        return services;
    }
}
