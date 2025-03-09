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
            CreateMap<InventoryItem, InventoryItemDto>().ReverseMap();
            CreateMap<InventoryCategory, InventoryCategoryDto>().ReverseMap();
            CreateMap<InventoryAttribute, InventoryAttributeDto>().ReverseMap();

            // Map from DTO to Entity
            CreateMap<ErrorFeedbackDto, ErrorFeedback>()
                .ForMember(dest => dest.ErrorJson, opt => opt.MapFrom(src =>
                    JsonSerializer.Serialize(src.Error, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    })));

            // Map from Entity to DTO
            CreateMap<ErrorFeedback, ErrorFeedbackDto>()
                .ForMember(dest => dest.Error, opt => opt.MapFrom(src =>
                    JsonSerializer.Deserialize<ErrorDetailsDto>(src.ErrorJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new ErrorDetailsDto()));
        }
    }
}
