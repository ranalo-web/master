namespace Ranalo.Services
{
    // The amount a contract finances (excluding the deposit), counting daily,
    // weekly and monthly payers -- the same sum as
    // ContractCalculatorService.CalculateOutstandingAmount, so
    // TotalLoan + Deposit - TotalPaid = LoanBalance.
    // Until 2026-11-06 the reports used Daily * 30 * term only, so monthly
    // payers had a TotalLoan of 0 and showed e.g. "100% paid" with money
    // still owed, in the weekly summary message too.
    public static class ContractMath
    {
        public static decimal TotalLoan(decimal daily, decimal weekly, decimal monthly, decimal termInMonths) =>
            daily * 30 * termInMonths + weekly * (30m / 7m) * termInMonths + monthly * termInMonths;
    }
}
