namespace Ranalo.Models
{
    public class WatchlistViewModel
    {
        public List<AccountWatchlistListItem> Watchlist { get; set; } = new();
        public List<AllAccounts> SearchResults { get; set; } = new();
        public string SearchTerm { get; set; } = "";
    }
}
