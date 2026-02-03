namespace Ranalo.Models
{
    public class DealersViewModel
    {
        public List<DataStore.Dealer>? Dealers { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
    }
}
