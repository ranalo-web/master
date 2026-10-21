namespace Ranalo.Models
{
    // A screening call logged against an order (CustomerScreeningCalls).
    public class ScreeningCall
    {
        public const string Customer = "Customer";
        public const string NextOfKin = "NextOfKin";

        public int Id { get; set; }
        public long OrderId { get; set; }
        public string CallType { get; set; } = Customer;
        public byte? NextOfKinNo { get; set; }
        public bool Reached { get; set; }
        public bool IdentityConfirmed { get; set; }
        public bool TermsConfirmed { get; set; }
        public bool LockUnderstood { get; set; }
        public bool KnowsCustomer { get; set; }
        public bool RelationshipConfirmed { get; set; }
        public string? Relationship { get; set; }
        public string? Notes { get; set; }
        public int CalledByUserId { get; set; }
        public string? CalledByName { get; set; }
        public DateTime CalledAtUtc { get; set; }
    }

    // A welfare call logged against an account (CustomerWelfareCalls).
    public class WelfareCall
    {
        public const string AllGood = "AllGood";
        public const string Issue = "Issue";
        public const string NoAnswer = "NoAnswer";

        public int Id { get; set; }
        public long AccountNo { get; set; }
        public string Outcome { get; set; } = AllGood;
        public string? Notes { get; set; }
        public int CalledByUserId { get; set; }
        public string? CalledByName { get; set; }
        public DateTime CalledAtUtc { get; set; }
    }

    public enum ScreeningStatus
    {
        NotRequired,   // order from before screening started
        NotStarted,
        InProgress,    // calls logged, not passed yet
        Passed
    }

    public enum WelfareStatus
    {
        NotDue,        // under 7 days since enrolment
        Due,           // day 7 to day 30
        Overdue,       // past day 30
        Done
    }

    // The screening panel on the order page.
    public class OrderScreening
    {
        public long OrderId { get; set; }
        public ScreeningStatus Status { get; set; }
        public bool CustomerPassed { get; set; }
        public bool NextOfKinPassed { get; set; }
        public List<ScreeningCall> Calls { get; set; } = new();
    }

    public class ScreeningOrderRow
    {
        public long OrderId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? DealerRef { get; set; }
        public DateTime DateCreated { get; set; }
        public int Attempts { get; set; }
        public bool CustomerPassed { get; set; }
        public bool NextOfKinPassed { get; set; }
        public DateTime? LastCallUtc { get; set; }
    }

    public class WelfareRow
    {
        public long AccountNo { get; set; }
        public long OrderId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public DateTime EnrolledAtUtc { get; set; }
        public int DaysSinceEnrolment { get; set; }
        public WelfareStatus Status { get; set; }
        public int Attempts { get; set; }
        public DateTime? LastCallUtc { get; set; }
    }

    public class NotPayingRow
    {
        public long AccountNo { get; set; }
        public string? FirstName { get; set; }
        public DateTime? FirstPaymentDate { get; set; }
        public int DaysSinceStart { get; set; }
        public decimal Daily { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Arrears { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public bool Locked { get; set; }
    }

    public class CustomerCallsViewModel
    {
        public List<NotPayingRow> NotPaying { get; set; } = new();
        public List<ScreeningOrderRow> ToScreen { get; set; } = new();
        public List<WelfareRow> Welfare { get; set; } = new();
    }
}
