namespace Ranalo.Models
{
    public class DealersViewModel
    {
        public List<DataStore.Dealer>? Dealers { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; } = 20;
        public string? SearchTerm { get; set; }
        // Two tabs: Open dealers (the default) and Closed ones (DealerRules).
        public bool ShowClosed { get; set; }
        public int OpenCount { get; set; }
        public int ClosedCount { get; set; }

        // How each dealer is doing (SalesTeamRules) and when their dealer
        // login was last seen, by DealerId.
        public Dictionary<int, Ranalo.Services.SalesTeamStats> Stats { get; set; } = new();
        public Dictionary<int, DateTime?> LastSeen { get; set; } = new();

        public static readonly int[] PageSizes = { 10, 20, 50, 100 };

        // Search by name, reference, id, email or phone; newest dealer first.
        public static DealersViewModel Page(IEnumerable<DataStore.Dealer> dealers, int page, int pageSize, string? searchTerm, bool showClosed = false)
        {
            var term = (searchTerm ?? "").Trim();
            pageSize = PageSizes.Contains(pageSize) ? pageSize : 20;

            var all = dealers.ToList();
            var matches = all
                .Where(d => Ranalo.Services.DealerRules.IsClosed(d.CompanyName) == showClosed)
                .Where(d => term == ""
                    || Has(d.CompanyName, term) || Has(d.DealerReference, term) || Has(d.Email, term)
                    || Has(d.Phone, term) || d.DealerId.ToString() == term)
                .OrderByDescending(d => d.DealerId)
                .ToList();

            var totalPages = Math.Max(1, (int)Math.Ceiling(matches.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            return new DealersViewModel
            {
                Dealers = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                CurrentPage = page,
                TotalPages = totalPages,
                TotalRecords = matches.Count,
                PageSize = pageSize,
                SearchTerm = term,
                ShowClosed = showClosed,
                ClosedCount = all.Count(d => Ranalo.Services.DealerRules.IsClosed(d.CompanyName)),
                OpenCount = all.Count(d => !Ranalo.Services.DealerRules.IsClosed(d.CompanyName)),
            };
        }

        private static bool Has(string? value, string term) =>
            value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
