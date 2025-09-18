namespace Ranalo.Models
{
    public class AllAccountsViewModel
    {
        public List<AllAccounts>? Accounts { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
    }
}
