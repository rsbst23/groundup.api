namespace GroundUp.core.dtos
{
    public class FilterParams : PaginationParams
    {
        public Dictionary<string, string>? Filters { get; set; } // Key-Value pairs of field names & values
    }
}
