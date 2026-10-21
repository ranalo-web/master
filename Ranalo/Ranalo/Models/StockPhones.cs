namespace Ranalo.Models
{
    // Stock phones page (Controllers/StockPhonesController.cs).
    public class StockPhonesViewModel
    {
        public string Filter { get; set; } = Services.DeviceLock.StockPhoneRules.FilterAll;
        public string? Search { get; set; }
        public List<Enrolment> Rows { get; set; } = new();
        public int Total { get; set; }
        public int Unverified { get; set; }
        public Dictionary<int, string> Dealers { get; set; } = new();
        // Admins/approvers pick the dealer; a dealer's phones are their own.
        public bool ChooseDealer { get; set; }
        public List<string> Messages { get; } = new();
        public List<string> Errors { get; } = new();
    }
}
