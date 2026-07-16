namespace KTrading.Services
{
    public static class SalesOrderFinancials
    {
        public static decimal CalculateNetAmount(
            decimal totalAmount,
            decimal commission,
            decimal dsrSalary,
            decimal damageAmount,
            decimal otherCosting,
            decimal khajnanAmount)
        {
            return Math.Max(totalAmount - commission - dsrSalary - damageAmount - otherCosting - khajnanAmount, 0m);
        }

        public static decimal CalculatePaidAmount(
            decimal totalAmount,
            decimal commission,
            decimal dsrSalary,
            decimal damageAmount,
            decimal otherCosting,
            decimal dueAmount,
            decimal khajnanAmount)
        {
            return Math.Max(CalculateNetAmount(totalAmount, commission, dsrSalary, damageAmount, otherCosting, khajnanAmount) - dueAmount, 0m);
        }
    }
}
