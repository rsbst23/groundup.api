using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;
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
        }
    }
}