using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOrders
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
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
        public List<Product> ProductsFull { get; set; } = new();
        public Dictionary<Guid, decimal> ProductStockMap { get; set; } = new();
        public Dictionary<Guid, decimal> ReturnedQuantityByProduct { get; set; } = new();
        public Dictionary<Guid, decimal> SalesAdjustmentQuantityByProduct { get; set; } = new();
        public decimal ReturnedAmount { get; set; }
        [BindProperty]
        public decimal DueAmountInput { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var loaded = await LoadOrderAsync(id);
            if (!loaded) return NotFound();

            await LoadListsAsync(id);
            ReturnedAmount = await CalculateReturnedAmountAsync(id);
            DueAmountInput = Math.Max(SalesOrder.Total - ReturnedAmount - SalesOrder.PaidAmount, 0m);
            ApplyReturnAdjustedDisplayItems();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var existingOrder = await _db.SalesOrders.FindAsync(id);
            if (existingOrder is null) return NotFound();

            SalesOrder.Id = id;
            await ConvertPostedNetItemsToGrossAsync(id);
            ValidateItems();
            if (ModelState.IsValid)
            {
                await ValidateReturnedQuantitiesAsync(id);
            }
            if (ModelState.IsValid)
            {
                await ValidateStockAvailabilityAsync(id);
            }
            if (ModelState.IsValid)
            {
                await ValidateDueAmountAsync(id);
            }

            if (!ModelState.IsValid)
            {
                await LoadListsAsync(id);
                ReturnedAmount = await CalculateReturnedAmountAsync(id);
                DueAmountInput = Math.Max(DueAmountInput, 0m);
                ApplyReturnAdjustedDisplayItems();
                return Page();
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            var existingItems = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == id)
                .ToListAsync();
            var oldOutMovements = await _db.StockMovements
                .Where(m => m.ReferenceId == id && m.MovementType == "OUT")
                .ToListAsync();

            foreach (var oldItem in existingItems)
            {
                var stock = await GetOrCreateStockAsync(oldItem.ProductId);
                stock.Quantity += oldItem.Quantity;
                stock.UpdatedAt = DateTimeOffset.UtcNow;
            }

            _db.StockMovements.RemoveRange(oldOutMovements);
            _db.SalesOrderItems.RemoveRange(existingItems);

            var subtotal = 0m;
            foreach (var item in Items)
            {
                item.Id = Guid.NewGuid();
                item.SalesOrderId = id;
                item.LineTotal = item.Quantity * item.UnitPrice;
                subtotal += item.LineTotal;

                _db.SalesOrderItems.Add(item);
                _db.StockMovements.Add(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    Quantity = -item.Quantity,
                    MovementType = "OUT",
                    ReferenceId = id,
                    Note = "Sale edit",
                    CreatedAt = DateTimeOffset.UtcNow
                });

                var stock = await GetOrCreateStockAsync(item.ProductId);
                stock.Quantity -= item.Quantity;
                stock.UpdatedAt = DateTimeOffset.UtcNow;
            }

            existingOrder.OrderNumber = SalesOrder.OrderNumber;
            existingOrder.CustomerId = SalesOrder.CustomerId;
            existingOrder.SalesOfficerId = SalesOrder.SalesOfficerId;
            existingOrder.OrderDate = SalesOrder.OrderDate;
            existingOrder.Subtotal = subtotal;
            existingOrder.Tax = SalesOrder.Tax;
            existingOrder.Discount = SalesOrder.Discount;
            existingOrder.Total = Math.Max(subtotal + SalesOrder.Tax - SalesOrder.Discount, 0m);
            existingOrder.Commission = Math.Max(SalesOrder.Commission, 0m);
            var returnedAmount = await CalculateReturnedAmountAsync(id, Items);
            var adjustedTotal = Math.Max(existingOrder.Total - returnedAmount, 0m);
            existingOrder.PaidAmount = Math.Max(adjustedTotal - DueAmountInput, 0m);
            existingOrder.DueAmount = Math.Max(existingOrder.Total - existingOrder.PaidAmount, 0m);
            existingOrder.UpdatedAt = DateTimeOffset.UtcNow;

            await ReconcileInitialPaymentAsync(existingOrder);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToPage("Details", new { id });
        }

        private async Task<bool> LoadOrderAsync(Guid id)
        {
            var order = await _db.SalesOrders.FindAsync(id);
            if (order is null) return false;

            SalesOrder = order;
            Items = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == id)
                .ToListAsync();

            return true;
        }

        private void ValidateItems()
        {
            Items = Items
                .Where(i => i.ProductId != Guid.Empty || i.Quantity > 0 || i.UnitPrice > 0)
                .ToList();

            if (Items.Count == 0)
            {
                ModelState.AddModelError(nameof(Items), "Add at least one product before saving the sales order.");
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

                if (Items[i].UnitPrice < 0)
                {
                    ModelState.AddModelError($"Items[{i}].UnitPrice", "Unit price cannot be negative.");
                }
            }
        }

        private void ApplyReturnAdjustedDisplayItems()
        {
            Items = Items
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var soldQuantity = g.Sum(i => i.Quantity);
                    var soldAmount = g.Sum(i => i.LineTotal);
                    var unitPrice = soldQuantity == 0 ? 0 : soldAmount / soldQuantity;
                    var salesAdjustmentQuantity = SalesAdjustmentQuantityByProduct.GetValueOrDefault(g.Key);

                    return new SalesOrderItem
                    {
                        Id = g.First().Id,
                        SalesOrderId = g.First().SalesOrderId,
                        ProductId = g.Key,
                        Quantity = Math.Max(soldQuantity - salesAdjustmentQuantity, 0m),
                        UnitPrice = unitPrice,
                        LineTotal = Math.Max(soldAmount - (salesAdjustmentQuantity * unitPrice), 0m)
                    };
                })
                .OrderBy(i => i.ProductId)
                .ToList();
        }

        private async Task ConvertPostedNetItemsToGrossAsync(Guid salesOrderId)
        {
            var returnedByProduct = await GetSalesAdjustmentQuantitiesAsync(salesOrderId);

            foreach (var item in Items)
            {
                var returnedQuantity = returnedByProduct.GetValueOrDefault(item.ProductId);
                item.Quantity += returnedQuantity;
                item.LineTotal = item.Quantity * item.UnitPrice;
            }
        }

        private async Task ValidateReturnedQuantitiesAsync(Guid salesOrderId)
        {
            var requestedByProduct = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var returnedByProduct = await GetReturnedQuantitiesAsync(salesOrderId);

            foreach (var returned in returnedByProduct)
            {
                var requested = requestedByProduct.GetValueOrDefault(returned.Key);
                if (requested >= returned.Value)
                {
                    continue;
                }

                var product = await _db.Products.FindAsync(returned.Key);
                ModelState.AddModelError(nameof(Items), $"{product?.Name ?? "Selected product"} already has {returned.Value:N2} returned, so the sold quantity cannot be lower than that.");
            }
        }

        private async Task ValidateStockAvailabilityAsync(Guid salesOrderId)
        {
            var requestedByProduct = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var existingByProduct = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);
            var productIds = requestedByProduct.Keys.ToList();
            var productNames = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name);
            var availableByProduct = await _db.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, s => s.Quantity);

            foreach (var request in requestedByProduct)
            {
                var available = availableByProduct.GetValueOrDefault(request.Key)
                    + existingByProduct.GetValueOrDefault(request.Key);
                if (request.Value <= available)
                {
                    continue;
                }

                var productName = productNames.GetValueOrDefault(request.Key, "Selected product");
                ModelState.AddModelError(nameof(Items), $"{productName} has only {available:N2} available for this edit, but the order needs {request.Value:N2}.");
            }
        }

        private async Task ValidateDueAmountAsync(Guid salesOrderId)
        {
            if (DueAmountInput < 0)
            {
                ModelState.AddModelError(nameof(DueAmountInput), "Due amount cannot be negative.");
                return;
            }

            var subtotal = Items.Sum(i => i.Quantity * i.UnitPrice);
            var total = Math.Max(subtotal + SalesOrder.Tax - SalesOrder.Discount, 0m);
            var returnedAmount = await CalculateReturnedAmountAsync(salesOrderId, Items);
            var adjustedTotal = Math.Max(total - returnedAmount, 0m);
            if (DueAmountInput > adjustedTotal)
            {
                ModelState.AddModelError(nameof(DueAmountInput), $"Due amount cannot be greater than adjusted total ({adjustedTotal:N2}).");
                return;
            }

            var nonInitialPayments = await _db.Payments
                .Where(p => p.SalesOrderId == salesOrderId
                    && (p.Reference == null || !p.Reference.StartsWith("Initial payment for ")))
                .SumAsync(p => p.Amount);
            var paidAmount = Math.Max(adjustedTotal - DueAmountInput, 0m);

            if (paidAmount < nonInitialPayments)
            {
                ModelState.AddModelError(nameof(DueAmountInput), $"Due amount makes paid amount lower than existing due collections ({nonInitialPayments:N2}).");
            }
        }

        private async Task ReconcileInitialPaymentAsync(SalesOrder order)
        {
            var payments = await _db.Payments
                .Where(p => p.SalesOrderId == order.Id)
                .ToListAsync();
            var initialPayment = payments.FirstOrDefault(p => p.Reference != null && p.Reference.StartsWith("Initial payment for "));
            var otherPaymentsTotal = payments
                .Where(p => p.Id != initialPayment?.Id)
                .Sum(p => p.Amount);
            var initialAmount = Math.Max(order.PaidAmount - otherPaymentsTotal, 0m);

            if (initialAmount == 0)
            {
                if (initialPayment is not null)
                {
                    _db.Payments.Remove(initialPayment);
                }

                return;
            }

            if (initialPayment is null)
            {
                _db.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = order.Id,
                    PaymentDate = order.OrderDate,
                    Amount = initialAmount,
                    Reference = $"Initial payment for {order.OrderNumber}"
                });
                return;
            }

            initialPayment.PaymentDate = order.OrderDate;
            initialPayment.Amount = initialAmount;
            initialPayment.Reference = $"Initial payment for {order.OrderNumber}";
        }

        private async Task<Stock> GetOrCreateStockAsync(Guid productId)
        {
            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId);
            if (stock is not null)
            {
                return stock;
            }

            stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Stocks.Add(stock);
            return stock;
        }

        private async Task LoadListsAsync(Guid? salesOrderId = null)
        {
            CustomerList = await _db.Customers
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();
            SalesOfficerList = await _db.SalesOfficers
                .Where(o => o.IsActive || o.Id == SalesOrder.SalesOfficerId)
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
            ReturnedQuantityByProduct = salesOrderId.HasValue
                ? await GetReturnedQuantitiesAsync(salesOrderId.Value)
                : new Dictionary<Guid, decimal>();
            SalesAdjustmentQuantityByProduct = salesOrderId.HasValue
                ? await GetSalesAdjustmentQuantitiesAsync(salesOrderId.Value)
                : new Dictionary<Guid, decimal>();

            if (salesOrderId.HasValue)
            {
                var currentOrderQuantities = await _db.SalesOrderItems
                    .Where(i => i.SalesOrderId == salesOrderId.Value)
                    .GroupBy(i => i.ProductId)
                    .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                    .ToListAsync();

                foreach (var item in currentOrderQuantities)
                {
                    ProductStockMap[item.ProductId] = ProductStockMap.GetValueOrDefault(item.ProductId) + item.Quantity;
                }
            }
        }

        private async Task<decimal> CalculateReturnedAmountAsync(Guid salesOrderId)
        {
            var salesItems = await _db.SalesOrderItems
                .Where(i => i.SalesOrderId == salesOrderId)
                .ToListAsync();
            return await CalculateReturnedAmountAsync(salesOrderId, salesItems);
        }

        private async Task<decimal> CalculateReturnedAmountAsync(Guid salesOrderId, IEnumerable<SalesOrderItem> salesItems)
        {
            var unitPrices = salesItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(i => i.Quantity) == 0 ? 0 : g.Sum(i => i.LineTotal) / g.Sum(i => i.Quantity));
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems.Sum(i => GetSalesAdjustmentQuantity(i) * unitPrices.GetValueOrDefault(i.ProductId));
        }

        private async Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesAsync(Guid salesOrderId)
        {
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        }

        private async Task<Dictionary<Guid, decimal>> GetSalesAdjustmentQuantitiesAsync(Guid salesOrderId)
        {
            var returnItems = await _db.ProductReturnItems
                .Join(_db.ProductReturns.Where(r => r.SalesOrderId == salesOrderId),
                    item => item.ProductReturnId,
                    ret => ret.Id,
                    (item, ret) => item)
                .Where(i => !i.IsOutsideSalesDamageReturn)
                .ToListAsync();

            return returnItems
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(GetSalesAdjustmentQuantity));
        }

        private static decimal GetSalesAdjustmentQuantity(ProductReturnItem item)
        {
            return item.Quantity + Math.Max(item.DamagedQuantity, 0m);
        }
    }
}
