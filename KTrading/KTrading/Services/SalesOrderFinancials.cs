namespace KTrading.Services
{
    public static class SalesOrderFinancials
    {
        public static decimal CalculateNetAmount(
            decimal totalAmount,
            decimal commission,
            decimal dsrSalary,
            decimal damageAmount,
            decimal otherCosting)
        {
            return Math.Max(totalAmount - commission - dsrSalary - damageAmount - otherCosting, 0m);
        }

        public static decimal CalculatePaidAmount(
            decimal totalAmount,
            decimal commission,
            decimal dsrSalary,
            decimal damageAmount,
            decimal otherCosting,
            decimal dueAmount)
        {
            return Math.Max(CalculateNetAmount(totalAmount, commission, dsrSalary, damageAmount, otherCosting) - dueAmount, 0m);
        }
    }
}
