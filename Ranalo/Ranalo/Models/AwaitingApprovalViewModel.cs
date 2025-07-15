namespace Ranalo.Models
{
    public class AwaitingApprovalViewModel
    {
        public List<AwaitingApprovalDto>? AwaitingApprovals { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
    }
}
