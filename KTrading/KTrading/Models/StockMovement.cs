namespace KTrading.Models
{
    public class StockMovement
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string MovementType { get; set; } = "IN"; // IN, OUT, RETURN, DAMAGE
        public Guid? ReferenceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? Note { get; set; }
        public Guid? ProductUnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal EnteredQuantity { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;
    }
}
