using Ranalo.Models;

namespace Ranalo.Services
{
    // Self-service restructure in the customer app (agreed 2026-11-06).
    //
    // A customer who is behind and not already on a plan can put themselves on a plan once per
    // contract; after that they have to call. The plans offered are ours,
    // never a number the customer types:
    //   Standard -- the original daily (all-in), until paid: the longest.
    //   Faster   -- 1.5 x that, paid sooner.
    // Never below the original daily: the same floor approvers have
    // (RestructurePermissions).
    //
    // Choosing a plan only makes it Pending. It starts when the first day's
    // payment arrives within 24 hours; otherwise it expires and doesn't use
    // up their one go. Starting saves it exactly as staff restructures are
    // saved (one plan per account, agreed from the moment they chose it), so
    // the lock jobs, reports and app treat it the same. Admin reviews each
    // one within 24 hours and can undo it, which restores the previous plan;
    // an undone plan still counts as their one go.
    public static class SelfRestructureRules
    {
        public static readonly TimeSpan PayWithin = TimeSpan.FromHours(24);
        public const decimal FasterFactor = 1.5m;

        public record Option(string Name, decimal Daily, int Days, string Summary);

        public class Facts
        {
            public bool HasOpenContract { get; set; }
            public bool FullyPaid { get; set; }
            public decimal Balance { get; set; }
            // Behind on the agreed plan when there is one, else on the contract.
            public decimal Behind { get; set; }
            public decimal DailyAll { get; set; }
            public bool UsedBefore { get; set; }
            // Already on a plan (agreed with staff, or one they chose): only
            // a person changes it (2026-11-06).
            public bool OnPlan { get; set; }
            public bool LostOrStolen { get; set; }
            public bool InRecovery { get; set; }
        }

        // Why not, or null when they can choose a plan.
        public static string? Refusal(Facts f)
        {
            if (!f.HasOpenContract) return "There's no open contract on this account.";
            if (f.FullyPaid || f.Balance <= 0) return "Your phone is fully paid.";
            if (f.UsedBefore) return "You've already set up a payment plan yourself on this contract. Call us to agree a new one.";
            if (f.OnPlan) return "You're already on a payment plan. To change it, call us.";
            if (f.LostOrStolen || f.InRecovery) return "We can't set up a plan in the app for this account. Call us.";
            if (f.DailyAll <= 0) return "We can't work out a plan for this account in the app. Call us.";
            if (f.Behind <= 0) return "You're not behind, so there's nothing to fix.";
            return null;
        }

        public static List<Option> Options(Facts f)
        {
            if (Refusal(f) != null) return new();

            var standard = Math.Ceiling(f.DailyAll);
            var faster = Math.Ceiling(f.DailyAll * FasterFactor);
            var options = new List<Option> { Make("Standard", standard, f.Balance) };
            // Only worth offering when it really is shorter.
            if (Days(f.Balance, faster) < Days(f.Balance, standard) - 7) options.Add(Make("Faster", faster, f.Balance));
            return options;
        }

        public static Option? Find(Facts f, string? name) =>
            Options(f).FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));

        private static Option Make(string name, decimal daily, decimal balance)
        {
            var days = Days(balance, daily);
            return new(name, daily, days, $"Ksh {CustomerMessages.Money(daily)} a day for about {Describe(days)}, until paid");
        }

        public static int Days(decimal balance, decimal daily) => daily <= 0 ? 0 : (int)Math.Ceiling(balance / daily);

        private static string Describe(int days) =>
            days < 60 ? $"{days} days" : $"{Math.Round(days / 30.0)} months";

        // A pending plan starts once paid since choosing covers the first day,
        // within the 24 hours; past that it expires.
        public enum PendingOutcome { Wait, Start, Expire }

        public static PendingOutcome Decide(SelfRestructure pending, decimal paidSinceChosen, DateTime nowUtc) =>
            paidSinceChosen >= pending.Daily ? PendingOutcome.Start
            : nowUtc >= pending.ExpiresAtUtc ? PendingOutcome.Expire
            : PendingOutcome.Wait;
    }
}
