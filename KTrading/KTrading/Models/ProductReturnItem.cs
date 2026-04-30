namespace KTrading.Models
{
    public class ProductReturnItem
    {
        public Guid Id { get; set; }
        public Guid ProductReturnId { get; set; }
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
        public bool IsDamaged { get; set; }
        public string? Notes { get; set; }
    }
}
