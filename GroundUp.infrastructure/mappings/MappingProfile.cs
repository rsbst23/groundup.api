using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.infrastructure.mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<InventoryItem, InventoryItemDto>().ReverseMap();
            CreateMap<InventoryCategory, InventoryCategoryDto>().ReverseMap();
            CreateMap<InventoryAttribute, InventoryAttributeDto>().ReverseMap();
        }
    }
}
