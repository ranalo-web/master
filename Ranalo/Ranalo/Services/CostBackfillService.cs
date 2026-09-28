using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using Ranalo.Woocommece.Api.Services;

namespace Ranalo.Services
{
    public interface ICostBackfillService
    {
        Task<CostBackfillViewModel> BuildAsync(string rule);
        Task<int> ApplyAsync(IEnumerable<CostBackfillApplyRow> rows);
    }

    // Suggests a BuyingPrice for every contract that has none, from:
    //   order   -- the cost of goods saved on the contract's own order line
    //              (orders synced since the cost-of-goods fix); always used
    //              when present, whatever the rule, since it's what was paid
    //   woo     -- the product's current WooCommerce "Cost of goods"
    //              (the matching variation's for variable products)
    //   nearest / latest / average -- contracts for the same product (same
    //              RAM/storage when known), or failing that the same device
    //              make/model, that already have a BuyingPrice
    // The page pre-fills each row from the chosen rule; the admin reviews
    // and edits before anything is saved.
    public class CostBackfillService : ICostBackfillService
    {
        public static readonly IReadOnlyDictionary<string, string> Rules = new Dictionary<string, string>
        {
            ["woo"] = "WooCommerce cost of goods",
            ["nearest"] = "Similar device, nearest date",
            ["latest"] = "Similar device, latest",
            ["average"] = "Similar devices, average",
        };

        private readonly ICostBackfillRepository _repository;
        private readonly ISyncService _syncService;
        private readonly ILogger<CostBackfillService> _logger;

        public CostBackfillService(ICostBackfillRepository repository, ISyncService syncService, ILogger<CostBackfillService> logger)
        {
            _repository = repository;
            _syncService = syncService;
            _logger = logger;
        }

        public async Task<CostBackfillViewModel> BuildAsync(string rule)
        {
            var model = new CostBackfillViewModel { Rule = Rules.ContainsKey(rule) ? rule : "woo" };
            model.Contracts = await _repository.GetContractsMissingCostAsync();
            model.DevicesWithoutContracts = await _repository.GetDevicesWithoutContractsAsync();
            var known = await _repository.GetKnownCostsAsync();

            var wooCosts = new List<WooProductCost>();
            try
            {
                var productIds = model.Contracts.Where(c => c.ProductId > 0).Select(c => c.ProductId!.Value);
                wooCosts = await _syncService.GetProductCostsAsync(productIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cost backfill: fetching WooCommerce product costs failed");
                model.WooError = ex.Message;
            }
            var wooByProduct = wooCosts.ToLookup(w => w.ProductId);

            foreach (var row in model.Contracts)
            {
                if (row.OrderCost > 0)
                {
                    row.Suggestions.Add(new CostSuggestion { Source = "order", Label = "This order's cost of goods", Cost = row.OrderCost.Value });
                }

                if (row.ProductId > 0 && WooSuggestion(row, wooByProduct[row.ProductId.Value].ToList()) is { } woo)
                {
                    row.Suggestions.Add(woo);
                }

                row.Suggestions.AddRange(SimilarSuggestions(row, known));

                row.SelectedCost = (row.Suggestions.FirstOrDefault(s => s.Source == "order")
                                    ?? row.Suggestions.FirstOrDefault(s => s.Source == model.Rule))?.Cost;
            }

            return model;
        }

        public async Task<int> ApplyAsync(IEnumerable<CostBackfillApplyRow> rows)
        {
            var prices = rows
                .Where(r => r.Include && r.BuyingPrice > 0)
                .Select(r => (r.ContractId, Math.Round(r.BuyingPrice!.Value, 2)))
                .ToList();

            return prices.Count == 0 ? 0 : await _repository.ApplyBuyingPricesAsync(prices);
        }

        private static CostSuggestion? WooSuggestion(CostBackfillContractRow row, List<WooProductCost> costs)
        {
            var variations = costs.Where(c => c.VariationId != null && c.Cost > 0).ToList();
            if (variations.Count > 0)
            {
                var wanted = new[] { row.ProductRam, row.ProductStorage }
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(Normalise)
                    .ToList();

                var match = wanted.Count > 0
                    ? variations.Where(v => wanted.All(w => v.Attributes.Any(a => Normalise(a) == w))).ToList()
                    : new List<WooProductCost>();

                if (match.Select(m => m.Cost).Distinct().Count() == 1)
                {
                    return new CostSuggestion { Source = "woo", Label = $"WooCommerce cost ({string.Join(" / ", match[0].Attributes)})", Cost = match[0].Cost!.Value };
                }

                if (variations.Select(v => v.Cost).Distinct().Count() == 1)
                {
                    return new CostSuggestion { Source = "woo", Label = "WooCommerce cost (all variations)", Cost = variations[0].Cost!.Value };
                }
            }

            var product = costs.FirstOrDefault(c => c.VariationId == null && c.Cost > 0);
            return product == null
                ? null
                : new CostSuggestion { Source = "woo", Label = "WooCommerce cost of goods", Cost = product.Cost!.Value };
        }

        private static IEnumerable<CostSuggestion> SimilarSuggestions(CostBackfillContractRow row, List<CostBackfillKnownCost> known)
        {
            List<CostBackfillKnownCost> similar;
            string basis;
            if (row.ProductId > 0)
            {
                similar = known.Where(k => k.ProductId == row.ProductId
                                           && SameOrUnknown(k.ProductRam, row.ProductRam)
                                           && SameOrUnknown(k.ProductStorage, row.ProductStorage)).ToList();
                basis = "same product";
            }
            else
            {
                similar = new List<CostBackfillKnownCost>();
                basis = "";
            }

            if (similar.Count == 0 && !string.IsNullOrWhiteSpace(row.DeviceModel))
            {
                similar = known.Where(k => Normalise(k.DeviceMake) == Normalise(row.DeviceMake)
                                           && Normalise(k.DeviceModel) == Normalise(row.DeviceModel)).ToList();
                basis = "same model";
            }

            if (similar.Count == 0)
            {
                yield break;
            }

            var target = row.StartDate ?? DateTime.Now;
            var nearest = similar.OrderBy(k => Math.Abs(((k.StartDate ?? target) - target).TotalDays)).First();
            var latest = similar.OrderByDescending(k => k.StartDate ?? DateTime.MinValue).First();

            yield return new CostSuggestion { Source = "nearest", Label = $"Nearest {basis} ({nearest.StartDate:dd MMM yyyy})", Cost = nearest.BuyingPrice };
            yield return new CostSuggestion { Source = "latest", Label = $"Latest {basis} ({latest.StartDate:dd MMM yyyy})", Cost = latest.BuyingPrice };
            yield return new CostSuggestion { Source = "average", Label = $"Average of {similar.Count} ({basis})", Cost = Math.Round(similar.Average(k => k.BuyingPrice), 0) };
        }

        private static bool SameOrUnknown(string? a, string? b) =>
            string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || Normalise(a) == Normalise(b);

        private static string Normalise(string? value) =>
            new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
