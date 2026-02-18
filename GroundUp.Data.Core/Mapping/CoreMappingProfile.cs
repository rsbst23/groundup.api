using AutoMapper;
using GroundUp.Core.dtos;
using GroundUp.Core.dtos.tenants;
using GroundUp.Core.entities;

namespace GroundUp.Data.Core.Mapping;

/// <summary>
/// Core AutoMapper profile for mapping EF entities to DTOs used by repositories/services.
/// Keeps mapping configuration close to the data layer.
/// </summary>
public sealed class CoreMappingProfile : Profile
{
    public CoreMappingProfile()
    {
        // Tenant
        CreateMap<Tenant, TenantDetailDto>();
        CreateMap<Tenant, TenantListItemDto>();

        // User/Tenant link
        CreateMap<UserTenant, UserTenantDto>();

        // Permissions/Roles/Policies
        CreateMap<Permission, PermissionDto>();
        CreateMap<Role, RoleDto>();
        CreateMap<Policy, PolicyDto>();
        CreateMap<RolePolicy, RolePolicyDto>();

        // Error feedback
        CreateMap<ErrorFeedback, ErrorFeedbackDto>();
    }
}
