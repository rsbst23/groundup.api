namespace GroundUp.core.dtos
{
    public class PaginationParams
    {
        private const int MaxPageSize = 100; // Prevents large queries
        public int PageNumber { get; set; } = 1;

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }

        public string? SortBy { get; set; } = "Id"; // Default sorting column
    }
}
