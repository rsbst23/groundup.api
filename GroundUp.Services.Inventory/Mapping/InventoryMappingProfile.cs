using AutoMapper;
using GroundUp.core.dtos;
using GroundUp.Repositories.Inventory.Entities;

namespace GroundUp.Services.Inventory.Mapping;

public sealed class InventoryMappingProfile : Profile
{
    public InventoryMappingProfile()
    {
        CreateMap<InventoryCategory, InventoryCategoryDto>().ReverseMap();
        CreateMap<InventoryItem, InventoryItemDto>().ReverseMap();
        CreateMap<InventoryAttribute, InventoryAttributeDto>().ReverseMap();
    }
}
