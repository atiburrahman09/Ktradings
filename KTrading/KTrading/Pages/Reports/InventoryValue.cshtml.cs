using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        [BindProperty(SupportsGet = true)]
        public Guid? SelectedCategoryId { get; set; }

        public List<SelectListItem> CategoryList { get; set; } = new();

        public async Task OnGetAsync()
        {
            CategoryList = await _db.ProductCategories
                            .OrderBy(c => c.Name)
                            .Select(c => new SelectListItem
                            {
                                Value = c.Id.ToString(),   // Guid converted to string
                                Text = c.Name
                            })
                            .ToListAsync();
            var query = from s in _db.Stocks
                        join p in _db.Products
                            .Include(x => x.ProductCategory)
                        on s.ProductId equals p.Id
                        select new { Product = p, Stock = s };

            if (SelectedCategoryId.HasValue)
            {
                query = query.Where(x => x.Product.ProductCategoryId == SelectedCategoryId);
            }

            query = query
                    .OrderBy(x => x.Product.ProductCategory != null
                        ? x.Product.ProductCategory.Name
                        : "Uncategorized")
                    .ThenBy(x => x.Product.Name);

            var items = await query.ToListAsync();

            TotalValue = 0m;
            var rows = items.Select(i =>
            {
                var qty = i.Stock.Quantity;
                var cost = i.Product.Price > 0
                    ? i.Product.Price
                    : i.Product.Cost;
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
