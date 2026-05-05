namespace KTrading.Models
{
    public class PaginationModel
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }

        public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
