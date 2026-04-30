namespace KTrading.Models
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid? SalesOrderId { get; set; }
        public int? PaymentMethodId { get; set; }
        public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }
}
