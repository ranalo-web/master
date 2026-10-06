using System.Text.RegularExpressions;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.Services.DeviceLock
{
    // Device enrolment and lock-removal rules (agreed with the business
    // 2026-10-06). Kept free of I/O so they can be unit tested; see
    // Database/DeviceLock/001 for the tables.
    //
    // Enrolment (dealer, approver or admin uploads an IMEI against an order)
    //   The brand comes from the WooCommerce order's product name, never from
    //   the person enrolling, and decides the lock provider: Samsung -> Knox,
    //   itel/TECNO/Infinix -> Transsion PayTrigger. The IMEI's TAC (first 8
    //   digits) is checked against TACs we've already confirmed for a brand;
    //   a known TAC that disagrees with the order blocks the upload. An
    //   unrecognised product brand blocks too. Only an admin can override.
    //   The order's M-Pesa deposit ref must be in our payments, on this
    //   customer's account. Less than the required deposit doesn't block, but
    //   flags the enrolment so only an admin can approve it (they then
    //   restructure the contract from day 1).
    //   An approver's enrolment is registered to the dealer on the order.
    //   One phone per account: a second phone uses the ID number with 1, 2
    //   or 3 added at the end as its account.
    //
    // Approval call (Transsion)
    //   The phone is pre-enrolled to lock as soon as it's switched on. It
    //   only unlocks once both: the customer has switched it on and connected
    //   (PayTrigger webhook), and an approver/admin has called the customer
    //   and approved -- in either order. An approver can't approve an
    //   enrolment they made themselves; an admin has to.
    //
    // Fully paid
    //   Lock date goes to 31/12/9999 straight away so the customer isn't held
    //   up, and a removal task is queued. Only an admin can approve removing
    //   the device from its lock provider.
    public static class DeviceLockRules
    {
        public const string Samsung = "Samsung";
        public const string Itel = "itel";
        public const string Tecno = "TECNO";
        public const string Infinix = "Infinix";

        public static readonly IReadOnlyList<string> KnownBrands = new[] { Samsung, Itel, Tecno, Infinix };

        // 31/12/9999 23:59:59 UTC -- what a fully-paid device's lock date is set to.
        public static readonly DateTime FullyPaidLockDateUtc = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Sent to Transsion if it refuses 9999 (not yet confirmed either way).
        public static readonly DateTime FullyPaidFallbackLockDateUtc = new(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Default product-name keywords, used when the DeviceBrandKeywords
        // table is empty. Matched as whole words, case-insensitive.
        public static readonly IReadOnlyList<BrandKeyword> DefaultBrandKeywords = new[]
        {
            new BrandKeyword("Samsung", Samsung),
            new BrandKeyword("Galaxy", Samsung),
            new BrandKeyword("itel", Itel),
            new BrandKeyword("TECNO", Tecno),
            new BrandKeyword("Techno", Tecno),   // common misspelling on the store
            new BrandKeyword("Camon", Tecno),
            new BrandKeyword("Spark", Tecno),
            new BrandKeyword("Infinix", Infinix),
        };

        public static bool IsTranssion(string? brand) => brand is Itel or Tecno or Infinix;

        public static LockProvider ProviderForBrand(string? brand) =>
            brand == Samsung ? LockProvider.Knox
            : IsTranssion(brand) ? LockProvider.Transsion
            : LockProvider.Nuovo;

        public static LockProvider ProviderForLockGroup(int? lockGroup) => lockGroup switch
        {
            2 => LockProvider.Knox,
            3 => LockProvider.Transsion,
            _ => LockProvider.Nuovo
        };

        public static string? NormaliseBrand(string? brand) =>
            KnownBrands.FirstOrDefault(b => string.Equals(b, brand?.Trim(), StringComparison.OrdinalIgnoreCase));

        // ---- Brand ---------------------------------------------------------

        // Returns the single brand named in the product name, or null when it
        // names none or more than one (ambiguous is treated as unknown).
        public static string? DetectBrand(string? productName, IEnumerable<BrandKeyword> keywords)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return null;
            }

            var brands = keywords
                .Where(k => !string.IsNullOrWhiteSpace(k.Keyword) &&
                            Regex.IsMatch(productName, $@"(?<![A-Za-z0-9]){Regex.Escape(k.Keyword.Trim())}(?![A-Za-z0-9])", RegexOptions.IgnoreCase))
                .Select(k => NormaliseBrand(k.Brand))
                .Where(b => b != null)
                .Distinct()
                .ToList();

            return brands.Count == 1 ? brands[0] : null;
        }

        public static string? Tac(string? imei) =>
            imei != null && imei.Length >= 8 && imei.Take(8).All(char.IsDigit) ? imei[..8] : null;

        public static BrandCheckResult CheckBrand(string? orderBrand, string? tacBrand)
        {
            if (orderBrand == null)
            {
                return new BrandCheckResult(BrandCheck.UnknownBrand,
                    "The product on this order doesn't name a brand we recognise (Samsung, itel, TECNO or Infinix). An admin must confirm the brand.");
            }

            if (tacBrand != null && !string.Equals(tacBrand, orderBrand, StringComparison.OrdinalIgnoreCase))
            {
                return new BrandCheckResult(BrandCheck.Mismatch,
                    $"The order is for a {orderBrand} phone but this IMEI belongs to a {tacBrand} phone. Check the IMEI; an admin must confirm the brand to continue.");
            }

            return tacBrand == null
                ? new BrandCheckResult(BrandCheck.TacUnverified, null)
                : new BrandCheckResult(BrandCheck.Ok, null);
        }

        // ---- Account number ------------------------------------------------

        // The account (= device id = contract id) is the national ID. A
        // customer's second, third... phone uses the ID with 1, 2, 3 added at
        // the end. One phone per account, so a new phone on an account that
        // still has one is stopped.
        public static string? CheckAccountFree(long accountId, string imei, string? liveImeiOnAccount) =>
            // Devices.Id is held as an int in the app.
            accountId <= 0 || accountId > int.MaxValue
                ? $"{accountId} is too long for an account number. Use the ID number plus one digit (1, 2 or 3) at most."
            : string.IsNullOrEmpty(liveImeiOnAccount) || liveImeiOnAccount == imei
                ? null
                : $"Account {accountId} already has phone {liveImeiOnAccount}. For a second phone the customer's account " +
                  $"is their ID number with 1, 2 or 3 added at the end (e.g. {accountId}2) – put that on the order and enrol again.";

        // ---- Deposit -------------------------------------------------------

        // Same formula the contract is created with (SyncService.CreateContractSingle).
        public static decimal RequiredDeposit(decimal totalAmount, decimal? dailySalePrice) =>
            dailySalePrice.HasValue
                ? totalAmount - (dailySalePrice.Value * 365)
                : Math.Round((Math.Round(totalAmount, 2) + 5000m) * 0.235m, 2);

        public static DepositCheckResult CheckDeposit(
            string? mpesaDepositRef,
            string accountNo,
            decimal requiredDeposit,
            IEnumerable<DepositPayment> paymentsWithRef)
        {
            if (string.IsNullOrWhiteSpace(mpesaDepositRef))
            {
                return new DepositCheckResult(DepositCheck.MissingRef, 0, requiredDeposit,
                    "This order has no M-Pesa deposit reference.");
            }

            var payments = paymentsWithRef
                .Where(p => string.Equals(p.MpesaCode?.Trim(), mpesaDepositRef.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (payments.Count == 0)
            {
                return new DepositCheckResult(DepositCheck.NotFound, 0, requiredDeposit,
                    $"The deposit M-Pesa code {mpesaDepositRef} hasn't been received yet. Try again once the payment has come in.");
            }

            var onAccount = payments.Where(p => p.AccountNo?.Trim() == accountNo).ToList();
            if (onAccount.Count == 0)
            {
                return new DepositCheckResult(DepositCheck.WrongAccount, 0, requiredDeposit,
                    $"The deposit M-Pesa code {mpesaDepositRef} was paid to a different account, not {accountNo}.");
            }

            var paid = onAccount.Sum(p => p.Amount);
            return paid >= requiredDeposit
                ? new DepositCheckResult(DepositCheck.Ok, paid, requiredDeposit, null)
                : new DepositCheckResult(DepositCheck.Short, paid, requiredDeposit,
                    $"Deposit paid ({paid:N2}) is less than the required {requiredDeposit:N2}. Only an admin can approve this enrolment.");
        }

        // ---- Who can do what -----------------------------------------------

        public static bool CanEnrol(UserRole role) =>
            role is UserRole.Admin or UserRole.Approver or UserRole.Dealer;

        public static bool CanOverrideBrand(UserRole role) => role == UserRole.Admin;

        public static ApprovalDecision CanApproveEnrolment(UserRole role, int userId, Enrolment enrolment)
        {
            if (role != UserRole.Admin && role != UserRole.Approver)
            {
                return ApprovalDecision.No("Only an approver or admin can approve an enrolment.");
            }

            if (role == UserRole.Approver && enrolment.EnrolledByUserId == userId)
            {
                return ApprovalDecision.No("You enrolled this device, so an admin must approve it.");
            }

            if (role == UserRole.Approver && enrolment.DepositShort)
            {
                return ApprovalDecision.No("The deposit is below the required amount, so only an admin can approve it.");
            }

            if (enrolment.CallApprovedAt.HasValue)
            {
                return ApprovalDecision.No("This enrolment has already been approved.");
            }

            if (enrolment.Status is EnrolmentStatus.Error or EnrolmentStatus.Approved)
            {
                return ApprovalDecision.No("This enrolment can't be approved in its current state.");
            }

            return ApprovalDecision.Yes;
        }

        public static bool CanDecideRemovals(UserRole role) => role == UserRole.Admin;

        // ---- Transsion enrolment state -------------------------------------

        public static bool ReadyToUnlock(Enrolment enrolment) =>
            enrolment.ActivatedAt.HasValue &&
            enrolment.CallApprovedAt.HasValue &&
            !enrolment.UnlockedAt.HasValue;

        // Status while waiting on the phone and/or the approval call.
        public static EnrolmentStatus WaitingStatus(Enrolment enrolment) =>
            enrolment.ActivatedAt.HasValue ? EnrolmentStatus.Enrolled : EnrolmentStatus.PendingActivation;

        public static string StatusText(Enrolment enrolment) => enrolment.Status switch
        {
            EnrolmentStatus.New => "New",
            EnrolmentStatus.Pending => "Pending",
            EnrolmentStatus.PendingActivation => enrolment.CallApprovedAt.HasValue
                ? "Approved – waiting for customer to switch on"
                : "Waiting for customer to switch on",
            EnrolmentStatus.Enrolled => "Enrolled & active – awaiting approval call",
            EnrolmentStatus.Approved => DeviceLockRules.IsTranssion(enrolment.DeviceBrand) ? "Approved – unlocked" : "Approved",
            EnrolmentStatus.Locked => "Locked",
            EnrolmentStatus.Error => "Error",
            _ => "No Status"
        };
    }

    public enum LockProvider
    {
        Nuovo = 1,
        Knox = 2,
        Transsion = 3
    }

    public record BrandKeyword(string Keyword, string Brand);

    public enum BrandCheck { Ok, TacUnverified, Mismatch, UnknownBrand }

    public record BrandCheckResult(BrandCheck Result, string? Error)
    {
        public bool Blocks => Result is BrandCheck.Mismatch or BrandCheck.UnknownBrand;
    }

    public record DepositPayment(string? MpesaCode, string? AccountNo, decimal Amount);

    public enum DepositCheck { Ok, Short, MissingRef, NotFound, WrongAccount }

    public record DepositCheckResult(DepositCheck Result, decimal Paid, decimal Required, string? Message)
    {
        public bool Blocks => Result is DepositCheck.MissingRef or DepositCheck.NotFound or DepositCheck.WrongAccount;
        public bool IsShort => Result == DepositCheck.Short;
    }

    public record ApprovalDecision(bool Allowed, string? Reason)
    {
        public static readonly ApprovalDecision Yes = new(true, null);
        public static ApprovalDecision No(string reason) => new(false, reason);
    }
}
