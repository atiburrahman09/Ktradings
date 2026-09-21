namespace KTrading.Models
{
    public class SalesOrderItem
    {
        public Guid Id { get; set; }
        public Guid SalesOrderId { get; set; }
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public Guid? ProductUnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;
        public decimal BaseQuantity { get; set; }
    }
}
