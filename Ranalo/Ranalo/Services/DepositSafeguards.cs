namespace Ranalo.Services
{
    // Two guards against a phone enrolled and unlocked with no contract
    // behind it (account 30702957, 2026-10-26: the order's deposit code
    // didn't match the payment, so no contract was made and the phone would
    // have relocked though the customer was paying). Kept free of I/O so it
    // can be unit tested.
    //
    // Enrolment: the dealer picks the deposit from the payments already on
    // the customer's account instead of typing the code.
    // Approved, no contract: every approved enrolment still without a
    // contract after ContractGrace, with what's stopping it.
    public static class DepositSafeguards
    {
        // The contract job runs every 10 minutes; this leaves it a few runs.
        public static readonly TimeSpan ContractGrace = TimeSpan.FromMinutes(30);

        // Payments on an account a dealer can offer as an order's deposit:
        // not already another order's deposit. Newest first.
        public static List<AccountPayment> DepositChoices(IEnumerable<AccountPayment> payments, long orderId) =>
            payments
                .Where(p => p.DepositOnOrderId == null || p.DepositOnOrderId == orderId)
                .OrderByDescending(p => p.PaidAt)
                .ToList();

        // What's stopping the contract, in the order the contract job
        // checks it (WooOrderProductRepository.ContractEligibleSql).
        public static string WhyNoContract(ApprovedWithoutContract row)
        {
            if (string.IsNullOrWhiteSpace(row.DepositCode))
            {
                return "The order has no deposit M-Pesa code. Add it on No Orders.";
            }

            if (!row.DepositReceived)
            {
                return $"Deposit code {row.DepositCode} isn't in our payments. Check it against the customer's payments below and correct it on No Orders.";
            }

            if (row.DepositPaidToAccount?.Trim() != row.AccountId.ToString())
            {
                return $"Deposit {row.DepositCode} was paid to account {row.DepositPaidToAccount}, not {row.AccountId}. Assign it on Assign Payments.";
            }

            if (string.IsNullOrWhiteSpace(row.DeviceStatus))
            {
                return $"There's no device on account {row.AccountId}.";
            }

            return "It should qualify: the contract job will create it on its next run (every 10 minutes).";
        }
    }

    public class AccountPayment
    {
        public string MpesaCode { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        // The order this payment is already the deposit for, if any.
        public long? DepositOnOrderId { get; set; }
    }

    public class ApprovedWithoutContract
    {
        public long AccountId { get; set; }
        public long OrderId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DeviceBrand { get; set; }
        public DateTime ApprovedDate { get; set; }
        public string? DepositCode { get; set; }
        public bool DepositReceived { get; set; }
        public string? DepositPaidToAccount { get; set; }
        public string? DeviceStatus { get; set; }

        // Recent payments on the account, to spot the real deposit code.
        public List<AccountPayment> RecentPayments { get; set; } = new();
    }
}
