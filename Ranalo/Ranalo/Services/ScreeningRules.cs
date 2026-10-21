using Ranalo.Models;

namespace Ranalo.Services
{
    // Customer screening and welfare calls (2026-10-21, Database/CustomerScreening/001).
    //
    // An order created from the screening start can only be approved once the
    // customer and one next of kin have been called: the customer confirming
    // identity, terms and the lock, the next of kin that they know the
    // customer and the relationship. Calls nobody answered are kept; the
    // order waits until it passes or is rejected. An order can only be
    // enrolled once approved. Orders from before the start carry on as they did.
    //
    // Once enrolled the customer gets a welfare call from day 7, overdue after
    // day 30. Anyone in arrears in the first 30 days of their contract goes to
    // the top of the approver's list.
    public static class ScreeningRules
    {
        public const string ApprovedStatus = "approved";
        public const int WelfareDueDay = 7;
        public const int WelfareOverdueDay = 30;
        public const int FirstMonthDays = 30;

        public static bool RequiresScreening(DateTime orderCreated, DateTime? screeningStartsUtc) =>
            screeningStartsUtc.HasValue && orderCreated >= screeningStartsUtc.Value;

        public static bool CustomerCallPassed(ScreeningCall c) =>
            c.CallType == ScreeningCall.Customer && c.Reached
            && c.IdentityConfirmed && c.TermsConfirmed && c.LockUnderstood;

        public static bool NextOfKinCallPassed(ScreeningCall c) =>
            c.CallType == ScreeningCall.NextOfKin && c.Reached
            && c.KnowsCustomer && c.RelationshipConfirmed;

        public static ScreeningStatus Status(bool required, IReadOnlyCollection<ScreeningCall> calls)
        {
            if (!required)
            {
                return ScreeningStatus.NotRequired;
            }

            if (calls.Any(CustomerCallPassed) && calls.Any(NextOfKinCallPassed))
            {
                return ScreeningStatus.Passed;
            }

            return calls.Count == 0 ? ScreeningStatus.NotStarted : ScreeningStatus.InProgress;
        }

        // null = can approve, otherwise why not.
        public static string? CannotApprove(ScreeningStatus status) => status switch
        {
            ScreeningStatus.NotRequired or ScreeningStatus.Passed => null,
            ScreeningStatus.NotStarted => "Screen the customer first: call the customer and one next of kin.",
            _ => "Screening isn't complete: the customer and one next of kin must both pass their calls."
        };

        // null = can enrol, otherwise why not.
        public static string? CannotEnrol(string? orderStatus, bool required) =>
            !required || string.Equals(orderStatus, ApprovedStatus, StringComparison.OrdinalIgnoreCase)
                ? null
                : "This order hasn't been approved yet. It must pass customer screening and be approved before it's enrolled.";

        public static int DaysSince(DateTime fromUtc, DateTime nowUtc) =>
            Math.Max(0, (int)(nowUtc - fromUtc).TotalDays);

        // A welfare call counts once someone answered (all good or an issue).
        public static WelfareStatus Welfare(DateTime enrolledUtc, DateTime nowUtc, IEnumerable<WelfareCall> calls)
        {
            if (calls.Any(c => c.Outcome is WelfareCall.AllGood or WelfareCall.Issue))
            {
                return WelfareStatus.Done;
            }

            var days = DaysSince(enrolledUtc, nowUtc);
            return days < WelfareDueDay ? WelfareStatus.NotDue
                : days > WelfareOverdueDay ? WelfareStatus.Overdue
                : WelfareStatus.Due;
        }

        public static bool NotPaying(DateTime? firstPaymentDate, decimal arrears, DateTime now) =>
            firstPaymentDate.HasValue
            && firstPaymentDate.Value >= now.AddDays(-FirstMonthDays)
            && arrears < 0;
    }
}
