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
        //public string? DepositMpesa { get; set; }
    }

    public enum EnrolmentStatus
    {
            New = 0,
            Pending = 1,
            Approved = 2,
            Locked = 3,
            Error = 4
    }
}
