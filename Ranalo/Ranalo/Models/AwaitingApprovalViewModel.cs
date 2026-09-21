namespace Ranalo.Models
{
    public class AwaitingApprovalViewModel
    {
        public List<AwaitingApprovalDto>? AwaitingApprovals { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public string? SearchTerm { get; set; }

        // Total matching rows across every page -- distinct from
        // AwaitingApprovals.Count, which is just this page's size (at most
        // PageSize). Several views were displaying the latter as "total
        // orders", which is only ever right on a single-page result.
        public int TotalRecords { get; set; }
    }
}
