using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductReturns
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
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

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var loaded = await LoadReturnAsync(id);
            if (!loaded) return NotFound();

            if (Return.Status == "Processed")
            {
                ModelState.AddModelError(string.Empty, "Processed returns cannot be edited because stock has already been updated.");
            }
            else if (SalesOrderId.HasValue && SalesOrderId.Value != Guid.Empty)
            {
                Return.SalesOrderId = SalesOrderId.Value;
                Items = new List<ProductReturnItem>();
            }

            await LoadPageAsync(Return.SalesOrderId, id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var existingReturn = await _db.ProductReturns.FindAsync(id);
            if (existingReturn is null) return NotFound();
            if (existingReturn.Status == "Processed")
            {
                ModelState.AddModelError(string.Empty, "Processed returns cannot be edited because stock has already been updated.");
                await LoadReturnAsync(id);
                await LoadPageAsync(existingReturn.SalesOrderId, id);
                return Page();
            }

            await LoadPageAsync(Return.SalesOrderId, id);
            if (!ModelState.IsValid) return Page();

            var salesOrder = await _db.SalesOrders.FindAsync(Return.SalesOrderId);
            if (salesOrder is null)
            {
                ModelState.AddModelError(nameof(Return.SalesOrderId), "Select a valid sales order.");
                return Page();
            }

            Items = Items
                .Where(i => i.ProductId != Guid.Empty && (i.Quantity > 0 || i.DamagedQuantity > 0))
                .ToList();

            if (!Items.Any())
            {
                ModelState.AddModelError(nameof(Items), "Enter a return quantity for at least one product from the selected sales order.");
                return Page();
            }

            var allowedByProduct = await GetReturnableQuantitiesAsync(salesOrder.Id, id);
            foreach (var itemGroup in Items.GroupBy(i => i.ProductId))
            {
                var allowed = allowedByProduct.GetValueOrDefault(itemGroup.Key);
                var requested = itemGroup.Sum(i => i.Quantity);
                var damaged = itemGroup.Sum(i => i.DamagedQuantity);

                if (damaged > requested)
                {
                    var damagedProduct = await _db.Products.FindAsync(itemGroup.Key);
                    ModelState.AddModelError(nameof(Items), $"{damagedProduct?.Name ?? "Selected product"} damaged quantity cannot be greater than return quantity.");
                    continue;
                }

                if (requested <= allowed)
                {
                    continue;
                }

                var product = await _db.Products.FindAsync(itemGroup.Key);
                ModelState.AddModelError(nameof(Items), $"{product?.Name ?? "Selected product"} can return only {allowed:N2} from this sales order.");
            }

            if (!ModelState.IsValid) return Page();

            existingReturn.ReturnNumber = Return.ReturnNumber;
            existingReturn.SalesOrderId = salesOrder.Id;
            existingReturn.CustomerId = salesOrder.CustomerId;
            existingReturn.Reason = Return.Reason;

            var oldItems = await _db.ProductReturnItems
                .Where(i => i.ProductReturnId == id)
                .ToListAsync();
            _db.ProductReturnItems.RemoveRange(oldItems);

            foreach (var item in Items)
            {
                item.Id = Guid.NewGuid();
                item.ProductReturnId = id;
                item.IsDamaged = item.DamagedQuantity > 0;
                _db.ProductReturnItems.Add(item);
            }

            await _db.SaveChangesAsync();
            return RedirectToPage("Details", new { id });
        }

        private async Task<bool> LoadReturnAsync(Guid id)
        {
            var ret = await _db.ProductReturns.FindAsync(id);
            if (ret is null) return false;

            Return = ret;
            Items = await _db.ProductReturnItems
                .Where(i => i.ProductReturnId == id)
                .ToListAsync();
            return true;
        }

        private async Task LoadPageAsync(Guid? salesOrderId, Guid currentReturnId)
        {
            SalesOrders = await _db.SalesOrders
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.CreatedAt)
                .Select(o => new SelectListItem(
                    $"{o.OrderNumber} - {o.OrderDate:yyyy-MM-dd}",
                    o.Id.ToString(),
                    o.Id == salesOrderId))
                .ToListAsync();

            if (salesOrderId is null || salesOrderId == Guid.Empty)
            {
                ReturnLines = new List<ReturnLineInput>();
                return;
            }

            SelectedSalesOrder = await _db.SalesOrders.FindAsync(salesOrderId.Value);
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
            var returnedByProduct = await GetReturnedQuantitiesAsync(SelectedSalesOrder.Id, currentReturnId);
            var currentItemsByProduct = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Quantity = g.Sum(i => i.Quantity),
                        DamagedQuantity = g.Sum(i => i.DamagedQuantity),
                        Notes = string.Join("; ", g.Select(i => i.Notes).Where(n => !string.IsNullOrWhiteSpace(n)))
                    });

            ReturnLines = orderItems
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var sold = g.Sum(i => i.Quantity);
                    var returned = returnedByProduct.GetValueOrDefault(g.Key);
                    currentItemsByProduct.TryGetValue(g.Key, out var current);
                    var currentQuantity = current?.Quantity ?? 0m;
                    return new ReturnLineInput
                    {
                        ProductId = g.Key,
                        ProductName = products.GetValueOrDefault(g.Key, g.Key.ToString()),
                        SoldQuantity = sold,
                        ReturnedQuantity = returned,
                        ReturnableQuantity = Math.Max(sold - returned, 0),
                        Quantity = currentQuantity,
                        DamagedQuantity = current?.DamagedQuantity ?? 0m,
                        Notes = current?.Notes,
                        MaxQuantity = Math.Max(sold - returned, 0)
                    };
                })
                .OrderBy(l => l.ProductName)
                .ToList();
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnableQuantitiesAsync(Guid salesOrderId, Guid currentReturnId)
        {
            var soldByProduct = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
            var returnedByProduct = await GetReturnedQuantitiesAsync(salesOrderId, currentReturnId);

            return soldByProduct.ToDictionary(
                sold => sold.Key,
                sold => Math.Max(sold.Value - returnedByProduct.GetValueOrDefault(sold.Key), 0));
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesAsync(Guid salesOrderId, Guid currentReturnId)
        {
            return await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId && r.Id != currentReturnId),
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
            public decimal MaxQuantity { get; set; }
            public decimal Quantity { get; set; }
            public decimal DamagedQuantity { get; set; }
            public string? Notes { get; set; }
        }
    }
}
