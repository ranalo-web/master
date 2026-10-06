using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services.DeviceLock
{
    // Gathers what the enrolment checks need (order products, TAC, deposit
    // payments, the order's dealer) and applies DeviceLockRules. The basic
    // order/ID/IMEI checks stay in EnrolmentsController.
    public interface IEnrolmentCheckService
    {
        Task<EnrolmentCheckOutcome> EvaluateAsync(Enrolment enrolment, CustomerDetails order, UserRole actorRole, string? overrideBrand);
    }

    public class EnrolmentCheckOutcome
    {
        // Problems that always stop the upload.
        public List<string> Errors { get; } = new();

        // Brand used for routing: the order's, or an admin's override.
        public string? Brand { get; set; }
        public string? ProductName { get; set; }
        public string? TacBrand { get; set; }
        public bool BrandOverridden { get; set; }

        // Brand couldn't be confirmed. Stops the upload unless the device is
        // already on Nuovo (non-Samsung/Transsion phones are enrolled there).
        public bool BrandBlocked { get; set; }
        public string? BrandError { get; set; }

        public DepositCheckResult Deposit { get; set; } = null!;

        // Dealers.DealerId matching the order's referral code, if any.
        public int? OrderDealerId { get; set; }
    }

    public class EnrolmentCheckService : IEnrolmentCheckService
    {
        private readonly IDeviceLockRepository _repository;

        public EnrolmentCheckService(IDeviceLockRepository repository)
        {
            _repository = repository;
        }

        public async Task<EnrolmentCheckOutcome> EvaluateAsync(Enrolment enrolment, CustomerDetails order, UserRole actorRole, string? overrideBrand)
        {
            var outcome = new EnrolmentCheckOutcome();

            var accountError = DeviceLockRules.CheckAccountFree(
                enrolment.AccountId, enrolment.IMEI, await _repository.GetLiveDeviceImeiAsync(enrolment.AccountId));
            if (accountError != null)
            {
                outcome.Errors.Add(accountError);
            }

            await CheckBrandAsync(outcome, enrolment, order, actorRole, overrideBrand);
            await CheckDepositAsync(outcome, enrolment, order);

            if (!string.IsNullOrWhiteSpace(order.DealerRef))
            {
                outcome.OrderDealerId = await _repository.GetDealerIdByReferenceAsync(order.DealerRef);
            }

            return outcome;
        }

        private async Task CheckBrandAsync(EnrolmentCheckOutcome outcome, Enrolment enrolment, CustomerDetails order, UserRole actorRole, string? overrideBrand)
        {
            var keywords = await _repository.GetBrandKeywordsAsync();
            if (keywords.Count == 0)
            {
                keywords = DeviceLockRules.DefaultBrandKeywords.ToList();
            }

            // An order can carry accessories as well as the phone: use the
            // brand only if every product that names one agrees.
            var products = await _repository.GetOrderProductNamesAsync(order.OrderID);
            var detected = products
                .Select(p => (Product: p, Brand: DeviceLockRules.DetectBrand(p, keywords)))
                .ToList();
            var brands = detected.Where(d => d.Brand != null).Select(d => d.Brand).Distinct().ToList();
            var orderBrand = brands.Count == 1 ? brands[0] : null;

            outcome.ProductName = detected.FirstOrDefault(d => d.Brand != null).Product ?? products.FirstOrDefault();

            var tac = DeviceLockRules.Tac(enrolment.IMEI);
            outcome.TacBrand = tac == null ? null : await _repository.GetTacBrandAsync(tac);

            if (!string.IsNullOrWhiteSpace(overrideBrand))
            {
                if (!DeviceLockRules.CanOverrideBrand(actorRole))
                {
                    outcome.Errors.Add("Only an admin can override the device brand.");
                    return;
                }

                var brand = DeviceLockRules.NormaliseBrand(overrideBrand);
                if (brand == null)
                {
                    outcome.Errors.Add($"'{overrideBrand}' isn't a brand we can enrol.");
                    return;
                }

                outcome.Brand = brand;
                outcome.BrandOverridden = true;
                return;
            }

            var check = DeviceLockRules.CheckBrand(orderBrand, outcome.TacBrand);
            if (brands.Count > 1)
            {
                check = new BrandCheckResult(BrandCheck.UnknownBrand,
                    $"The products on this order name more than one brand ({string.Join(", ", brands)}). An admin must confirm the brand.");
            }

            outcome.Brand = orderBrand;
            outcome.BrandBlocked = check.Blocks;
            outcome.BrandError = check.Error;
        }

        private async Task CheckDepositAsync(EnrolmentCheckOutcome outcome, Enrolment enrolment, CustomerDetails order)
        {
            var dailySalePrice = await _repository.GetOrderDailySalePriceAsync(order.OrderID);
            var required = DeviceLockRules.RequiredDeposit(order.TotalAmount, dailySalePrice);

            var payments = string.IsNullOrWhiteSpace(order.MpesaDepositRef)
                ? new List<DepositPayment>()
                : await _repository.GetPaymentsByMpesaCodeAsync(order.MpesaDepositRef);

            outcome.Deposit = DeviceLockRules.CheckDeposit(order.MpesaDepositRef, enrolment.AccountId.ToString(), required, payments);

            if (outcome.Deposit.Blocks)
            {
                outcome.Errors.Add(outcome.Deposit.Message!);
            }
        }
    }
}
