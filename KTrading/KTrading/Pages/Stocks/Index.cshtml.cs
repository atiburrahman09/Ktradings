using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Stocks
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public IEnumerable<Stock> Stocks { get; set; } = Array.Empty<Stock>();
        public Dictionary<Guid, string> ProductMap { get; set; } = new();
        public PaginationModel Pager { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            var query = _db.Stocks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.Trim();
                var matchingProductIds = await _db.Products
                    .Where(p =>
                        p.Name.Contains(search) ||
                        (p.SKU != null && p.SKU.Contains(search)) ||
                        (p.Description != null && p.Description.Contains(search)))
                    .Select(p => p.Id)
                    .ToListAsync();

                query = query.Where(s => matchingProductIds.Contains(s.ProductId));
            }

            var totalItems = await query.CountAsync();
            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = totalItems };
            Stocks = await query
                .OrderBy(s => s.ProductId)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var products = await _db.Products.ToListAsync();
            ProductMap = products.ToDictionary(p => p.Id, p => p.Name);
        }
    }
}
