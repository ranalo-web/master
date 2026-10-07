using Ranalo.DataStore.DataModels;

namespace Ranalo.Models
{
    public class EnrolmentViewModel
    {
        public List<Enrolment>? Enrolments { get; set; } = new List<Enrolment>();
        public int CurrentPage { get; set; }
        public int TotalPages =>
        (int)Math.Ceiling((double)TotalCount / PageSize);
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? SearchTerm { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Messages { get; set; } = new List<string>();

        // Lets the views ask "can the viewer approve this row?" without
        // re-implementing DeviceLockRules.
        public Func<Enrolment, Ranalo.Services.DeviceLock.ApprovalDecision>? CanApprove { get; set; }
        public bool IsAdmin { get; set; }
        public IEnumerable<Ranalo.DataStore.Dealer>? Dealers { get; set; }
    }

    public class Enrolment
    {
        public Guid Id { get; set; }
        public long AccountId { get; set; }
        public int OrderId { get; set; }
        public int DealerId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public required string IMEI { get; set; }
        public string? DeviceBrand { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public DateTime ApprovedDate { get; set; }
        public EnrolmentStatus Status { get; set; }
        public string? UpdatedBy { get; set; }
        public string? VeriTechTransId { get; set; }
        public string? VeriTechData { get; set; }
        public string? VeriTechStatus { get; set; }
        public string? VeriTechMessage { get; set; }
        public long? VeriTechCode { get; set; }
        public string? KnoxResponse { get; set; }
        public string? PayTriggerStatus { get; set; }
        public string? PayTriggerResponse { get; set; }
        //public string? DepositMpesa { get; set; }

        // Enrolment checks and approval (Database/DeviceLock/001). See
        // Services/DeviceLock/DeviceLockRules.cs for the rules.
        public int? EnrolledByUserId { get; set; }
        public int? EnrolledByRole { get; set; }
        public string? ProductName { get; set; }
        public string? DealerRef { get; set; }
        public string? TacBrand { get; set; }
        public int? BrandOverriddenByUserId { get; set; }
        public decimal? RequiredDeposit { get; set; }
        public decimal? DepositPaid { get; set; }
        public bool DepositShort { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public int? CallApprovedByUserId { get; set; }
        public DateTime? CallApprovedAt { get; set; }
        public DateTime? UnlockedAt { get; set; }
    }

    public enum EnrolmentStatus
    {
            New = 0,
            Pending = 1,
            Approved = 2,
            Locked = 3,
            Error = 4,
            // Transsion: pre-enrolled, waiting for the customer to switch the
            // phone on and connect (PayTrigger webhook confirms it).
            PendingActivation = 5,
            // Transsion: phone switched on and active, waiting for the
            // approver/admin call before it unlocks.
            Enrolled = 6
    }
}
