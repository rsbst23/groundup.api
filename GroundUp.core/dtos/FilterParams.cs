namespace GroundUp.core.dtos
{
    public class FilterParams : PaginationParams
    {
        public Dictionary<string, string>? Filters { get; set; } // Exact matches
        public Dictionary<string, string>? ContainsFilters { get; set; } // Contains filtering
        public Dictionary<string, string>? MinFilters { get; set; } // Minimum range filters
        public Dictionary<string, string>? MaxFilters { get; set; } // Maximum range filters
        public Dictionary<string, string>? MultiValueFilters { get; set; } // IN clause filtering
        public string? SearchTerm { get; set; }
    }
}
