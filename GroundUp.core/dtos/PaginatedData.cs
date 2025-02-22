namespace GroundUp.core.dtos
{
    public class PaginatedData<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }

        public PaginatedData(List<T> items, int pageNumber, int pageSize, int totalRecords)
        {
            Items = items;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalRecords = totalRecords;
            TotalPages = (totalRecords > 0 && pageSize > 0) ? (int)Math.Ceiling(totalRecords / (double)pageSize) : 1;
        }
    }
}
