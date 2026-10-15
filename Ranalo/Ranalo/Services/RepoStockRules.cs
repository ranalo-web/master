using Ranalo.Models;

namespace Ranalo.Services
{
    // Repo stock rules (agreed with the business 2026-10-15). Kept free of
    // I/O so they can be unit tested; see Database/RepoStock/001.
    //
    // A phone taken back (repossessed, or handed back by the customer) goes
    // into repo stock: its contract ends that day and the old customer's
    // payments are renamed "<account>_R" -- "_R2", "_R3"... if the account
    // has been through repo stock before -- so they stay with that contract.
    // The phone keeps its account number: for Nuovo phones the account is
    // Nuovo's device number, which can't change.
    //
    // When a buyer comes along, an admin sets the terms (deposit, daily
    // rate, term) and the selling dealer/agent. A new contract starts that
    // day on the same account, credited to the seller, and the old
    // contract's unpaid balance is proposed as a Repossessed write-off.
    public static class RepoStockRules
    {
        // "_R" the first time an account goes into repo stock, then "_R2"...
        public static string PaymentSuffix(int timesInStockBefore) =>
            timesInStockBefore <= 0 ? "_R" : $"_R{timesInStockBefore + 1}";

        public static string? CannotAdd(RepoStockLookup? account, string reason)
        {
            if (account == null)
                return "No device on that account.";
            if (account.InStockNow)
                return $"Account {account.AccountNo} is already in repo stock.";
            if (account.ContractId == null)
                return $"Account {account.AccountNo} has no open contract to end.";
            if (account.OpenCollectionsCaseId.HasValue)
                return $"Account {account.AccountNo} has an open collections case ({account.OpenCollectionsCaseId}). Return it in Collections Admin first.";
            if (!RepoStockReason.All.Contains(reason))
                return "Choose why the phone came back.";
            return null;
        }

        public static string? CannotSell(RepoStockItem item, RepoSale sale)
        {
            if (item.Status != RepoStockStatus.InStock)
                return "This phone isn't in stock any more.";
            if (string.IsNullOrWhiteSpace(sale.CustomerName))
                return "Enter the buyer's name.";
            if (sale.Deposit < 0)
                return "The deposit can't be negative.";
            if (sale.Daily <= 0)
                return "Enter the daily rate.";
            if (sale.TermInMonths is < 1 or > 36)
                return "The term must be between 1 and 36 months.";
            if (sale.DealerReference <= 0)
                return "Choose the selling dealer.";
            return null;
        }

        // Contract value as the rest of the app works it out (30-day months).
        public static decimal TotalLoan(decimal daily, decimal termInMonths) => Math.Round(daily * 30 * termInMonths, 2);
        public static decimal TotalCost(decimal deposit, decimal daily, decimal termInMonths) => deposit + TotalLoan(daily, termInMonths);

        // The old contract's unpaid balance: what it was worth less what the
        // customer paid. Never negative.
        public static decimal WriteOff(RepoOldContract old) => Math.Max(0, Math.Round(old.ContractValue - old.TotalPaid, 2));
    }
}
