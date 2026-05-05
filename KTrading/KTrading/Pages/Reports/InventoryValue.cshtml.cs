using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using KTrading.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

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
        public string ReportTitle { get; set; } = "Inventory Value";
        public IReadOnlyList<string> BusinessCategories { get; } = new[] { "Fresh", "Akij" };

        [BindProperty(SupportsGet = true)]
        public string? CategoryName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public async Task OnGetAsync()
        {
            const int pageSize = 10;
            PageNumber = Math.Max(PageNumber, 1);

            ReportTitle = string.IsNullOrWhiteSpace(CategoryName)
                ? "All Inventory Value"
                : $"{CategoryName} Products Inventory Value";

            var productsQuery = _db.Products.Include(p => p.ProductCategory).AsQueryable();
            if (!string.IsNullOrWhiteSpace(CategoryName))
            {
                productsQuery = productsQuery.Where(p => p.ProductCategory != null && p.ProductCategory.Name == CategoryName);
            }

            var products = await productsQuery
                .OrderBy(p => p.ProductCategory == null ? "Uncategorized" : p.ProductCategory.Name)
                .ThenBy(p => p.Name)
                .ToListAsync();
            var productIds = products.Select(p => p.Id).ToHashSet();
            var stocks = await _db.Stocks
                .ToListAsync();
            var prodMap = products.ToDictionary(p => p.Id, p => p);
            var rows = new List<Row>();
            foreach(var s in stocks.Where(s => productIds.Contains(s.ProductId)))
            {
                prodMap.TryGetValue(s.ProductId, out var p);
                var qty = s.Quantity;
                var cost = p?.Cost ?? 0m;
                var val = qty * cost;
                rows.Add(new Row { ProductName = p?.Name ?? s.ProductId.ToString(), Category = p?.ProductCategory?.Name ?? "Uncategorized", Quantity = qty, UnitCost = cost, Value = val });
                TotalValue += val;
            }

            Pager = new PaginationModel { PageNumber = PageNumber, PageSize = pageSize, TotalItems = rows.Count };
            Rows = rows
                .OrderBy(r => r.Category)
                .ThenBy(r => r.ProductName)
                .Skip((PageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
    }
}
