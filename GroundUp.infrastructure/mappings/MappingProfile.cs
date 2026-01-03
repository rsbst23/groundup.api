using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.dtos.tenants;
using GroundUp.core.entities;
using GroundUp.core.enums;
using System.Text.Json;

namespace GroundUp.infrastructure.mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Existing mappings
            CreateMap<InventoryItem, InventoryItemDto>().ReverseMap();
            CreateMap<InventoryCategory, InventoryCategoryDto>().ReverseMap();
            CreateMap<InventoryAttribute, InventoryAttributeDto>().ReverseMap();

            // Permission mappings
            CreateMap<Permission, PermissionDto>().ReverseMap();

            // Policy mappings
            CreateMap<Policy, PolicyDto>()
                .ForMember(dest => dest.Permissions, opt => opt.Ignore());

            CreateMap<PolicyDto, Policy>()
                .ForMember(dest => dest.PolicyPermissions, opt => opt.Ignore());

            // PolicyPermission mappings
            CreateMap<PolicyPermission, PolicyPermissionDto>()
                .ForMember(dest => dest.PolicyName, opt => opt.MapFrom(src => src.Policy.Name))
                .ForMember(dest => dest.PermissionName, opt => opt.MapFrom(src => src.Permission.Name));

            CreateMap<PolicyPermissionDto, PolicyPermission>();

            // Role mappings
            CreateMap<Role, RoleDto>().ReverseMap();

            // UserRole mappings
            CreateMap<UserRoleDto, UserRole>().ReverseMap();

            // RolePolicy mappings
            CreateMap<RolePolicy, RolePolicyDto>()
                .ForMember(dest => dest.PolicyName, opt => opt.MapFrom(src => src.Policy.Name));

            CreateMap<RolePolicyDto, RolePolicy>();

            // Error feedback mappings
            CreateMap<ErrorFeedbackDto, ErrorFeedback>()
                .ForMember(dest => dest.ErrorJson, opt => opt.MapFrom(src =>
                    JsonSerializer.Serialize(src.Error, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    })));

            CreateMap<ErrorFeedback, ErrorFeedbackDto>()
                .ForMember(dest => dest.Error, opt => opt.MapFrom(src =>
                    JsonSerializer.Deserialize<ErrorDetailsDto>(src.ErrorJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new ErrorDetailsDto()));

            // Tenant mappings
            CreateMap<Tenant, TenantListItemDto>()
                .ForMember(dest => dest.ParentTenantName, opt => opt.MapFrom(src => src.ParentTenant != null ? src.ParentTenant.Name : null));

            CreateMap<Tenant, TenantDetailDto>()
                .ForMember(dest => dest.ParentTenantName, opt => opt.MapFrom(src => src.ParentTenant != null ? src.ParentTenant.Name : null))
                .ForMember(dest => dest.SsoAutoJoinRoleName, opt => opt.MapFrom(src => src.SsoAutoJoinRole != null ? src.SsoAutoJoinRole.Name : null));

            CreateMap<CreateTenantDto, Tenant>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ParentTenant, opt => opt.Ignore())
                .ForMember(dest => dest.ChildTenants, opt => opt.Ignore())
                .ForMember(dest => dest.UserTenants, opt => opt.Ignore())
                .ForMember(dest => dest.TenantType, opt => opt.MapFrom(src => src.TenantType))
                .ForMember(dest => dest.RealmName, opt => opt.MapFrom(src => src.CustomDomain ?? "groundup"))
                .ForMember(dest => dest.SsoAutoJoinDomains, opt => opt.MapFrom(src => src.SsoAutoJoinDomains));

            CreateMap<UpdateTenantDto, Tenant>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ParentTenantId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ParentTenant, opt => opt.Ignore())
                .ForMember(dest => dest.ChildTenants, opt => opt.Ignore())
                .ForMember(dest => dest.UserTenants, opt => opt.Ignore())
                .ForMember(dest => dest.SsoAutoJoinDomains, opt => opt.MapFrom(src => src.SsoAutoJoinDomains));
        
            // UserTenant mappings
            CreateMap<UserTenant, UserTenantDto>().ReverseMap();

            // TenantInvitation mappings
            CreateMap<TenantInvitation, TenantInvitationDto>()
                .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : string.Empty))
                .ForMember(dest => dest.RealmName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.RealmName : null))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.ContactEmail))
                .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedByUser != null ? src.CreatedByUser.Username : string.Empty))
                .ForMember(dest => dest.AcceptedByUserName, opt => opt.MapFrom(src => src.AcceptedByUser != null ? src.AcceptedByUser.Username : string.Empty))
                .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => src.IsExpired));

            // User mappings
            CreateMap<User, UserSummaryDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.Enabled, opt => opt.MapFrom(src => src.IsActive));

            CreateMap<User, UserDetailsDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.Enabled, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.EmailVerified, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedTimestamp, opt => opt.Ignore())
                .ForMember(dest => dest.Attributes, opt => opt.Ignore())
                .ForMember(dest => dest.Groups, opt => opt.Ignore())
                .ForMember(dest => dest.RealmRoles, opt => opt.Ignore())
                .ForMember(dest => dest.ClientRoles, opt => opt.Ignore());

            // TenantJoinLink mappings
            CreateMap<TenantJoinLink, TenantJoinLinkDto>()
                .ForMember(dest => dest.JoinUrl, opt => opt.Ignore()) // Set by controller
                .ForMember(dest => dest.TenantName, opt => opt.MapFrom(src => src.Tenant != null ? src.Tenant.Name : null));
        }
    }
}