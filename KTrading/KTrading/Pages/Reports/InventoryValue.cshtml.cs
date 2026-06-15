using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Reports
{
    public class InventoryValueModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public InventoryValueModel(ApplicationDbContext db) { _db = db; }

        public class Row { public string ProductName { get; set; } = ""; public string Category { get; set; } = ""; public decimal Quantity { get; set; } public decimal UnitCost { get; set; } public decimal Value { get; set; } }

        public List<Row> Rows { get; set; } = new();
        public PaginationModel Pager { get; set; } = new();
        public decimal TotalValue { get; set; }
        public string ReportTitle { get; set; } = "Inventory Report";

        public async Task OnGetAsync()
        {
            // Join Stocks to Products and include category so EF does the correct lookups server-side.
            var query = from s in _db.Stocks
                        join p in _db.Products.Include(x => x.ProductCategory) on s.ProductId equals p.Id
                        orderby (p.ProductCategory != null ? p.ProductCategory.Name : "Uncategorized"), p.Name
                        select new { Product = p, Stock = s };

            var items = await query.ToListAsync();

            TotalValue = 0m;
            var rows = items.Select(i =>
            {
                var qty = i.Stock.Quantity;
                var cost = i.Product.Cost;
                var val = qty * cost;
                return new Row
                {
                    ProductName = i.Product.Name,
                    Category = i.Product.ProductCategory?.Name ?? "Uncategorized",
                    Quantity = qty,
                    UnitCost = cost,
                    Value = val
                };
            }).ToList();

            TotalValue = rows.Sum(r => r.Value);

            Pager = new PaginationModel { PageNumber = 1, PageSize = Math.Max(rows.Count, 1), TotalItems = rows.Count };
            Rows = rows.OrderBy(r => r.Category).ThenBy(r => r.ProductName).ToList();
        }
    }
}
