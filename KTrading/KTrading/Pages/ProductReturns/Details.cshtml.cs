using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductReturns
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DetailsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public ProductReturn? Return { get; set; }
        public IEnumerable<ProductReturnItem>? Items { get; set; }
        public Dictionary<Guid, string> ProductMap { get; set; } = new();
        public string? CustomerName { get; set; }
        public string? SalesOrderNumber { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Return = await _db.ProductReturns.FindAsync(id);
            if (Return is null) return NotFound();
            Items = await _db.ProductReturnItems.Where(i => i.ProductReturnId == id).ToListAsync();
            var products = await _db.Products.ToListAsync();
            ProductMap = products.ToDictionary(p => p.Id, p => p.Name);
            if (Return.CustomerId != null)
            {
                var c = await _db.Customers.FindAsync(Return.CustomerId.Value);
                CustomerName = c?.Name;
            }
            if (Return.SalesOrderId != null)
            {
                var order = await _db.SalesOrders.FindAsync(Return.SalesOrderId.Value);
                SalesOrderNumber = order?.OrderNumber;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostProcessAsync(Guid id)
        {
            var ret = await _db.ProductReturns.FindAsync(id);
            if (ret is null) return NotFound();
            if (ret.Status == "Processed")
            {
                return RedirectToPage("Details", new { id });
            }

            // Damaged returned quantity reduces sales but is not restocked.
            var items = await _db.ProductReturnItems.Where(i => i.ProductReturnId == id).ToListAsync();
            foreach(var it in items)
            {
                var damagedQuantity = GetDamagedQuantity(it);
                var restockQuantity = Math.Max(it.Quantity - damagedQuantity, 0m);

                if (damagedQuantity > 0)
                {
                    var sm = new StockMovement { Id = Guid.NewGuid(), ProductId = it.ProductId, Quantity = 0, MovementType = "DAMAGE", ReferenceId = id, Note = it.Notes, CreatedAt = DateTimeOffset.UtcNow };
                    _db.StockMovements.Add(sm);
                }

                if (restockQuantity > 0)
                {
                    var sm = new StockMovement { Id = Guid.NewGuid(), ProductId = it.ProductId, Quantity = restockQuantity, MovementType = "RETURN", ReferenceId = id, Note = it.Notes, CreatedAt = DateTimeOffset.UtcNow };
                    _db.StockMovements.Add(sm);

                    var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId);
                    if (stock == null)
                    {
                        stock = new Stock { Id = Guid.NewGuid(), ProductId = it.ProductId, Quantity = 0, UpdatedAt = DateTimeOffset.UtcNow };
                        _db.Stocks.Add(stock);
                    }
                    stock.Quantity += restockQuantity;
                    stock.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            ret.Status = "Processed";

            await _db.SaveChangesAsync();
            return RedirectToPage("Details", new { id = id });
        }

        public static decimal GetDamagedQuantity(ProductReturnItem item)
        {
            return item.DamagedQuantity > 0 ? item.DamagedQuantity : item.IsDamaged ? item.Quantity : 0m;
        }
    }
}
