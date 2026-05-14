using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace KTrading.Pages.SalesOrders
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CreateModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public SalesOrder SalesOrder { get; set; } = new();

        [BindProperty]
        public List<SalesOrderItem> Items { get; set; } = new();

        public IEnumerable<SelectListItem> CustomerList { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> SalesOfficerList { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> ProductCategoryList { get; set; } = Array.Empty<SelectListItem>();
        public List<KTrading.Models.Product> ProductsFull { get; set; } = new();
        public Dictionary<Guid, decimal> ProductStockMap { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadListsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadListsAsync();
                return Page();
            }

            ValidateItems();
            if (ModelState.IsValid)
            {
                await ValidateStockAvailabilityAsync();
            }

            if (!ModelState.IsValid)
            {
                await LoadListsAsync();
                return Page();
            }

            if (SalesOrder.Id == Guid.Empty) SalesOrder.Id = Guid.NewGuid();
            SalesOrder.CreatedAt = DateTimeOffset.UtcNow;

            // compute totals
            decimal subtotal = 0;
            foreach(var it in Items)
            {
                it.Id = Guid.NewGuid();
                it.SalesOrderId = SalesOrder.Id;
                it.LineTotal = it.Quantity * it.UnitPrice;
                subtotal += it.LineTotal;

                // create stock movement and decrease stock
                var sm = new StockMovement { Id = Guid.NewGuid(), ProductId = it.ProductId, Quantity = -it.Quantity, MovementType = "OUT", ReferenceId = SalesOrder.Id, Note = "Sale", CreatedAt = DateTimeOffset.UtcNow };
                _db.StockMovements.Add(sm);

                var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId);
                if (stock == null)
                {
                    stock = new Stock { Id = Guid.NewGuid(), ProductId = it.ProductId, Quantity = 0, UpdatedAt = DateTimeOffset.UtcNow };
                    _db.Stocks.Add(stock);
                }
                stock.Quantity -= it.Quantity;
                stock.UpdatedAt = DateTimeOffset.UtcNow;
            }
            SalesOrder.Subtotal = subtotal;
            SalesOrder.Total = subtotal + SalesOrder.Tax - SalesOrder.Discount;
            if (SalesOrder.Total < 0) SalesOrder.Total = 0;
            if (SalesOrder.Commission < 0) SalesOrder.Commission = 0;
            SalesOrder.Khajna = 0;
            SalesOrder.DsrSalary = 0;
            if (SalesOrder.PaidAmount < 0) SalesOrder.PaidAmount = 0;
            SalesOrder.DueAmount = SalesOrder.Total - SalesOrder.PaidAmount;
            if (SalesOrder.DueAmount < 0) SalesOrder.DueAmount = 0;

            _db.SalesOrders.Add(SalesOrder);
            if(Items.Any()) _db.SalesOrderItems.AddRange(Items);
            if (SalesOrder.PaidAmount > 0)
            {
                _db.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = SalesOrder.Id,
                    PaymentDate = SalesOrder.OrderDate,
                    Amount = SalesOrder.PaidAmount,
                    Reference = $"Initial payment for {SalesOrder.OrderNumber}"
                });
            }
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }

        private void ValidateItems()
        {
            if (Items.Count == 0)
            {
                ModelState.AddModelError(nameof(Items), "Add at least one product before creating the sales order.");
                return;
            }

            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].ProductId == Guid.Empty)
                {
                    ModelState.AddModelError($"Items[{i}].ProductId", "Select a product.");
                }

                if (Items[i].Quantity <= 0)
                {
                    ModelState.AddModelError($"Items[{i}].Quantity", "Quantity must be greater than zero.");
                }
            }
        }

        private async Task ValidateStockAvailabilityAsync()
        {
            var requestedByProduct = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            var productIds = requestedByProduct.Keys.ToList();
            var productNames = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            var availableByProduct = await _db.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.Quantity);

            foreach (var request in requestedByProduct)
            {
                var available = availableByProduct.GetValueOrDefault(request.Key);
                if (request.Value <= available)
                {
                    continue;
                }

                var productName = productNames.GetValueOrDefault(request.Key, "Selected product");
                ModelState.AddModelError(nameof(Items), $"{productName} has only {available:N2} in stock, but this order needs {request.Value:N2}.");
            }
        }

        private async Task LoadListsAsync()
        {
            CustomerList = await _db.Customers.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
            SalesOfficerList = await _db.SalesOfficers
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem(o.Name, o.Id.ToString()))
                .ToListAsync();
            ProductCategoryList = await _db.ProductCategories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();
            ProductsFull = await _db.Products
                .OrderBy(p => p.Name)
                .ToListAsync();
            ProductStockMap = await _db.Stocks.ToDictionaryAsync(s => s.ProductId, s => s.Quantity);
        }
    }
}
