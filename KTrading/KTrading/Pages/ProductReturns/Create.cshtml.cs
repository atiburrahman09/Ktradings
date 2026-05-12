using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductReturns
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CreateModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public ProductReturn Return { get; set; } = new();

        [BindProperty]
        public List<ProductReturnItem> Items { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public Guid? SalesOrderId { get; set; }

        public IEnumerable<SelectListItem> SalesOrders { get; set; } = Array.Empty<SelectListItem>();
        public SalesOrder? SelectedSalesOrder { get; set; }
        public string? CustomerName { get; set; }
        public List<ReturnLineInput> ReturnLines { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadPageAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadPageAsync(Return.SalesOrderId);
            if (!ModelState.IsValid) return Page();

            var salesOrder = await _db.SalesOrders.FindAsync(Return.SalesOrderId);
            if (salesOrder is null)
            {
                ModelState.AddModelError(nameof(Return.SalesOrderId), "Select a valid sales order.");
                return Page();
            }

            Items = Items
                .Where(i => i.ProductId != Guid.Empty && i.Quantity > 0)
                .ToList();

            if (!Items.Any())
            {
                ModelState.AddModelError(nameof(Items), "Enter a return quantity for at least one product from the selected sales order.");
                return Page();
            }

            var allowedByProduct = await GetReturnableQuantitiesAsync(salesOrder.Id);
            foreach (var itemGroup in Items.GroupBy(i => i.ProductId))
            {
                var allowed = allowedByProduct.GetValueOrDefault(itemGroup.Key);
                var requested = itemGroup.Sum(i => i.Quantity);

                if (requested <= allowed)
                {
                    continue;
                }

                var product = await _db.Products.FindAsync(itemGroup.Key);
                ModelState.AddModelError(nameof(Items), $"{product?.Name ?? "Selected product"} can return only {allowed:N2} from this sales order.");
            }

            if (!ModelState.IsValid) return Page();

            if (Return.Id == Guid.Empty) Return.Id = Guid.NewGuid();
            Return.CreatedAt = DateTimeOffset.UtcNow;
            Return.CustomerId = salesOrder.CustomerId;
            Return.SalesOrderId = salesOrder.Id;
            Return.Status = "Open";

            _db.ProductReturns.Add(Return);
            if (Items.Any())
            {
                foreach (var it in Items)
                {
                    it.Id = Guid.NewGuid();
                    it.ProductReturnId = Return.Id;
                }
                _db.ProductReturnItems.AddRange(Items);
            }
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }

        private async Task LoadPageAsync(Guid? salesOrderId = null)
        {
            SalesOrders = await _db.SalesOrders
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.CreatedAt)
                .Select(o => new SelectListItem(
                    $"{o.OrderNumber} - {o.OrderDate:yyyy-MM-dd}",
                    o.Id.ToString(),
                    o.Id == (salesOrderId ?? SalesOrderId)))
                .ToListAsync();

            var selectedId = salesOrderId ?? SalesOrderId;
            if (selectedId is null || selectedId == Guid.Empty)
            {
                ReturnLines = new List<ReturnLineInput>();
                return;
            }

            SelectedSalesOrder = await _db.SalesOrders.FindAsync(selectedId.Value);
            if (SelectedSalesOrder is null)
            {
                ReturnLines = new List<ReturnLineInput>();
                return;
            }

            Return.SalesOrderId = SelectedSalesOrder.Id;
            Return.CustomerId = SelectedSalesOrder.CustomerId;

            var customer = await _db.Customers.FindAsync(SelectedSalesOrder.CustomerId);
            CustomerName = customer?.Name;

            var orderItems = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == SelectedSalesOrder.Id)
                .ToListAsync();
            var productIds = orderItems.Select(i => i.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);
            var returnedByProduct = await GetReturnedQuantitiesAsync(SelectedSalesOrder.Id);

            ReturnLines = orderItems
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var sold = g.Sum(i => i.Quantity);
                    var returned = returnedByProduct.GetValueOrDefault(g.Key);
                    return new ReturnLineInput
                    {
                        ProductId = g.Key,
                        ProductName = products.GetValueOrDefault(g.Key, g.Key.ToString()),
                        SoldQuantity = sold,
                        ReturnedQuantity = returned,
                        ReturnableQuantity = Math.Max(sold - returned, 0)
                    };
                })
                .OrderBy(l => l.ProductName)
                .ToList();
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnableQuantitiesAsync(Guid salesOrderId)
        {
            var soldByProduct = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            var returnedByProduct = await GetReturnedQuantitiesAsync(salesOrderId);

            return soldByProduct.ToDictionary(
                sold => sold.Key,
                sold => Math.Max(sold.Value - returnedByProduct.GetValueOrDefault(sold.Key), 0));
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesAsync(Guid salesOrderId)
        {
            return await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
        }

        public class ReturnLineInput
        {
            public Guid ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public decimal SoldQuantity { get; set; }
            public decimal ReturnedQuantity { get; set; }
            public decimal ReturnableQuantity { get; set; }
        }
    }
}
