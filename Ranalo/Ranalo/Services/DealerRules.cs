using Ranalo.DataStore;

namespace Ranalo.Services
{
    // A closed dealer: one that stopped trading (never took off, company
    // book merged into Ranalo Credit, ...). It is closed by adding
    // " (closed)" to its name on the Dealers table. Closing is for good -- a
    // dealer who comes back is registered again as a new dealer -- so there
    // is no reopen and no extra column.
    //
    // Closed dealers are left out of every list where a dealer is chosen for
    // something new (enrolment, Move to dealer, stock and repo phones, user
    // logins, dealer filters). Reports and history still show them by name.
    public static class DealerRules
    {
        public const string ClosedSuffix = "(closed)";

        public static bool IsClosed(string? companyName) =>
            companyName != null && companyName.TrimEnd().EndsWith(ClosedSuffix, StringComparison.OrdinalIgnoreCase);

        public static List<Dealer> Open(IEnumerable<Dealer>? dealers) =>
            (dealers ?? Enumerable.Empty<Dealer>()).Where(d => !IsClosed(d.CompanyName)).ToList();
    }
}
