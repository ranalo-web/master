namespace Ranalo.Services
{
    // Collections rules (agreed with the business 2026-10-01). Kept free of
    // I/O so they can be unit tested; see Database/Collections/001 for the
    // tables.
    //
    // Handover
    //   Admin hands an account to a collector from the pool: in arrears and
    //   nothing paid for 90 days. The collector can't be the account's agent
    //   or (a login of) its dealer.
    //   The agent's and dealer's deduction on that contract is frozen at the
    //   shortfall on the day -- the collector's recoveries never reduce it.
    //   Their unpaid bonus on it is cancelled (bonus already paid is kept),
    //   payments while it is in collections add nothing to the dealer's
    //   commission, and the account counts toward their default rate for good.
    //
    // Collector pay
    //   20% of every payment on the device while they hold the case,
    //   including a new customer's payments after a repossession and resale.
    //   Reassigning the case starts the new collector earning from then on.
    //   No suspension: poor performance is handled by reassigning, and an
    //   account with no payment for 90 days while held is flagged for it.
    //
    // Return to the agent/dealer
    //   Deduction = frozen amount + what collectors earned on the case
    //   + repossession cost + any new shortfall after the return.
    //   Sent to collections again: re-frozen at that deduction.
    public static class CollectionsRules
    {
        public const decimal CollectorShareRate = 0.20m;

        // Pool: no payment for this many days (and in arrears).
        public const int EligibleDaysWithoutPayment = 90;

        // A held account with no payment for this many days can be reassigned.
        public const int ReassignAfterDaysWithoutPayment = 90;

        public static decimal CollectorShare(decimal recovered) => Math.Round(Math.Max(0, recovered) * CollectorShareRate, 2);

        public static bool IsEligible(decimal shortfall, int daysSinceLastPayment) =>
            shortfall > 0 && daysSinceLastPayment >= EligibleDaysWithoutPayment;

        public static bool IsDueForReassignment(int daysWithoutPaymentWhileHeld) =>
            daysWithoutPaymentWhileHeld >= ReassignAfterDaysWithoutPayment;

        // The agent's and the dealer's arrears deduction on a contract that
        // has been in collections (each carries all of it, as with ordinary
        // arrears).
        public static decimal Deduction(CollectionTerms t, decimal liveShortfall) =>
            t.IsReturned
                ? t.FrozenDeduction + t.CollectorEarned + t.RepossessionCost + Math.Max(0, liveShortfall - t.ShortfallAtReturn)
                : t.FrozenDeduction;

        // Customer payments that count toward the dealer's commission: what
        // counted at handover, plus payments after a return.
        public static decimal DealerCountedPaid(CollectionTerms t, decimal liveTotalPaid) =>
            t.IsReturned
                ? t.CommissionPaidBasis + Math.Max(0, liveTotalPaid - t.TotalPaidAtReturn)
                : t.CommissionPaidBasis;

        // The agent keeps only bonus already paid on a collections account.
        public static decimal AgentBonusKept(decimal standardBonus, decimal bonusAlreadyPaid) =>
            Math.Max(0, Math.Min(standardBonus, bonusAlreadyPaid));

        // Who can be handed an account: not its agent, and not a dealer login
        // of its dealer.
        public static bool IsConflicted(CollectorCandidate collector, int? accountAgentUserId, int? accountDealerId) =>
            (accountAgentUserId.HasValue && collector.UserId == accountAgentUserId.Value)
            || (collector.IsDealerUser && accountDealerId.HasValue && collector.DealerId == accountDealerId.Value);

        // Flag and call notes: trimmed, single-spaced, at most 500 characters
        // (the column size). Blank comes back as null.
        public const int NoteMaxLength = 500;

        public static string? CleanNote(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return null;
            var clean = string.Join(' ', note.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return clean.Length > NoteMaxLength ? clean[..NoteMaxLength] : clean;
        }

        // The same note already open from this collector on the case -- a
        // double click or a resubmit -- isn't flagged a second time.
        public static bool IsRepeatFlag(IEnumerable<string?> openNotesFromCollector, string note) =>
            openNotesFromCollector.Any(n => string.Equals(CleanNote(n), CleanNote(note), StringComparison.OrdinalIgnoreCase));

        // A promise to pay can't be in the past.
        public static bool IsValidPromiseDate(DateTime? promisedDate, DateTime today) =>
            promisedDate == null || promisedDate.Value.Date >= today.Date;

        // My Collections filters (2026-10-23). Worked on = the holder has
        // logged a call since taking the account, or it's flagged "can't
        // reach". Follow up = a call that's due a check: the promised date
        // has passed, or, with no promise, FollowUpAfterDays since the call.
        public const int FollowUpAfterDays = 7;

        public static class Filter
        {
            public const string All = "all";
            public const string Worked = "worked";
            public const string NotWorked = "notworked";
            public const string FollowUp = "followup";

            public static string Clean(string? value) =>
                value is Worked or NotWorked or FollowUp ? value : All;
        }

        public static bool HolderCalled(Ranalo.Models.CollectionCaseRow c) =>
            c.LastCall != null
            && c.LastCall.CollectorUserId == c.CollectorUserId
            && (c.HeldSince == null || c.LastCall.CreatedAt >= c.HeldSince);

        public static bool IsWorked(Ranalo.Models.CollectionCaseRow c) =>
            HolderCalled(c) || c.OpenFlagCount > 0;

        public static bool IsDueForFollowUp(Ranalo.Models.CollectionCaseRow c, DateTime today)
        {
            if (!HolderCalled(c)) return false;
            var call = c.LastCall!;
            return call.PromisedDate.HasValue
                ? today.Date > call.PromisedDate.Value.Date
                : today.Date >= call.CreatedAt.Date.AddDays(FollowUpAfterDays);
        }

        // A payment came in after the call.
        public static bool PaidSinceCall(Ranalo.Models.CollectionCaseRow c) =>
            c.LastCall != null && c.LastPaymentAt.HasValue && c.LastPaymentAt.Value > c.LastCall.CreatedAt;

        public static bool MatchesFilter(Ranalo.Models.CollectionCaseRow c, string filter, DateTime today) => filter switch
        {
            Filter.Worked => IsWorked(c),
            Filter.NotWorked => !IsWorked(c),
            Filter.FollowUp => IsDueForFollowUp(c, today),
            _ => true,
        };

        // My Collections search: account number, customer or next-of-kin
        // name, or phone. Phones match on their last 9 digits, so 0712 345678,
        // 254712345678 and +254 712 345 678 are the same number.
        public static bool MatchesSearch(Ranalo.Models.CollectionCaseRow c, string? term)
        {
            if (string.IsNullOrWhiteSpace(term)) return true;
            term = term.Trim();

            if (c.AccountNo.ToString().Contains(term)) return true;

            var names = new[] { c.CustomerName, c.Contact?.NextOfKinName, c.Contact?.NextOfKin2Name };
            if (names.Any(n => n != null && n.Contains(term, StringComparison.OrdinalIgnoreCase))) return true;

            var digits = PhoneKey(term);
            if (digits.Length < 4) return false;
            var phones = new[] { c.Contact?.CustomerPhone, c.Contact?.NextOfKinPhone, c.Contact?.NextOfKin2Phone };
            return phones.Any(p => p != null && PhoneKey(p).Contains(digits));
        }

        // The account's next of kin to show, first then second. The second is
        // left out when it's the same person again (same phone, or same name
        // with no phone) -- some orders store one contact twice.
        public static List<(string? Name, string? Phone, string? IdNumber)> NextOfKin(Ranalo.Models.AccountContact? contact)
        {
            var result = new List<(string? Name, string? Phone, string? IdNumber)>();
            if (contact == null) return result;

            foreach (var k in new[]
            {
                (Name: contact.NextOfKinName, Phone: contact.NextOfKinPhone, IdNumber: contact.NextOfKinIdNumber),
                (Name: contact.NextOfKin2Name, Phone: contact.NextOfKin2Phone, IdNumber: contact.NextOfKin2IdNumber),
            })
            {
                if (string.IsNullOrWhiteSpace(k.Name) && string.IsNullOrWhiteSpace(k.Phone)) continue;

                var repeat = result.Any(r =>
                    !string.IsNullOrWhiteSpace(k.Phone) && !string.IsNullOrWhiteSpace(r.Phone)
                        ? PhoneKey(k.Phone) == PhoneKey(r.Phone)
                        : string.Equals(k.Name?.Trim(), r.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!repeat) result.Add(k);
            }
            return result;
        }

        private static string PhoneKey(string value)
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length > 9 ? digits[^9..] : digits.TrimStart('0');
        }
    }

    // The latest collections case on one contract, as the commission
    // calculator needs it.
    public class CollectionTerms
    {
        public int CaseId { get; set; }
        public bool IsReturned { get; set; }
        public decimal FrozenDeduction { get; set; }
        public decimal CommissionPaidBasis { get; set; }

        // Only used once returned.
        public decimal ShortfallAtReturn { get; set; }
        public decimal TotalPaidAtReturn { get; set; }
        public decimal CollectorEarned { get; set; }
        public decimal RepossessionCost { get; set; }
    }

    public class CollectorCandidate
    {
        public int UserId { get; set; }
        public int DealerId { get; set; }

        // A dealer login (Role Dealer) who is also a collector.
        public bool IsDealerUser { get; set; }
    }
}
