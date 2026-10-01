using Ranalo.Models;

namespace Ranalo.DataStore
{
    // Commission payouts (Database/Commissions/002_create_commission_payouts.sql):
    // one CommissionPayouts row per payment, split per account into
    // AgentCommissionPayments / DealerCommissionPayments.
    public interface ICommissionPayoutRepository
    {
        // Saves the payout and its per-account lines in one transaction; returns the payout Id.
        Task<int> RecordAsync(NewCommissionPayout payout);

        // Newest first. Filters are optional; payeeType narrows to Agent or Dealer payouts.
        Task<List<CommissionPayoutRecord>> GetPayoutsAsync(string? payeeType = null, int? dealerId = null, int? agentUserId = null, int? top = null,
            int? collectorUserId = null);

        // Every payment line on these contracts from the payee's table, including
        // rows recorded before payouts existed (no method, maybe no reference).
        Task<List<CommissionAccountPayment>> GetAccountPaymentsAsync(string payeeType, IReadOnlyCollection<long> contractIds);

        // Marks a payout as received by the payee. False when it doesn't
        // belong to that payee or was already confirmed.
        Task<bool> ConfirmReceiptAsync(int payoutId, string payeeType, int payeeId, int confirmedByUserId);
    }
}
