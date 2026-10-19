using Ranalo.DataStore.DataModels;
using Ranalo.Models;
using Ranalo.PayTrigger.Models;
using Ranalo.SumsungKnox.Models;

namespace Ranalo.Services.DeviceLock
{
    // Stock phones (2026-10-19): an admin locks a phone with Knox or
    // Transsion by its IMEI alone -- no order, no M-Pesa -- so dealers and
    // agents can take it to the field locked. Knox and Transsion only need
    // the IMEI. When it's sold, the normal enrolment with the WooCommerce
    // order picks the stock enrolment up (AttachSale), and from there it's an
    // ordinary enrolment: approve, unlock, contract.
    //
    // A stock enrolment has no account (AccountId 0) and no Devices row, so
    // the payment and lock jobs never see it.
    public static class StockPhoneRules
    {
        public const int MaxPerBatch = 50;

        public const string FilterAll = "all";
        public const string FilterUnverified = "unverified";

        public static bool CanManage(UserRole role) => role == UserRole.Admin;

        public static bool IsStock(Enrolment e) => e.Status == EnrolmentStatus.Stock;

        // Started life as a stock phone (still stock, or sold since).
        public static bool FromStock(Enrolment e) => e.StockLockedAt.HasValue;

        public static bool NeedsVerifying(Enrolment e) => IsStock(e) && !e.StockVerifiedAt.HasValue;

        // IMEIs typed or pasted one per line (or separated by commas/spaces).
        public static (List<string> Imeis, List<string> Errors) ParseImeis(string? text)
        {
            var errors = new List<string>();
            var imeis = (text ?? "")
                .Split(new[] { '\n', '\r', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Distinct()
                .ToList();

            if (imeis.Count == 0)
            {
                errors.Add("Enter at least one IMEI.");
            }
            else if (imeis.Count > MaxPerBatch)
            {
                errors.Add($"Lock at most {MaxPerBatch} phones at a time.");
            }

            foreach (var bad in imeis.Where(i => !DeviceLockRules.IsValidImei(i)))
            {
                errors.Add($"{bad} isn't a valid IMEI.");
            }

            return (errors.Any() ? new List<string>() : imeis, errors);
        }

        // Only Knox (Samsung) and Transsion (itel/TECNO/Infinix) lock by IMEI.
        // Other brands are enrolled in Nuovo itself.
        public static string? CheckBrand(string? brand, string? tacBrand)
        {
            if (brand == null || DeviceLockRules.ProviderForBrand(brand) == LockProvider.Nuovo)
            {
                return "Choose the brand: Samsung (Knox), or itel, TECNO or Infinix (Transsion). Other phones are enrolled in Nuovo.";
            }

            return tacBrand != null && !string.Equals(tacBrand, brand, StringComparison.OrdinalIgnoreCase)
                ? $"This IMEI belongs to a {tacBrand} phone, not {brand}. Check the IMEI and the brand."
                : null;
        }

        // liveImeiAccount: the account the IMEI is live on in Devices, if any.
        public static string? CheckFree(Enrolment? existing, long? liveImeiAccount)
        {
            if (existing != null)
            {
                return IsStock(existing)
                    ? "This phone is already in stock."
                    : $"This phone is already enrolled (account {existing.AccountId}, order {existing.OrderId}).";
            }

            return liveImeiAccount.HasValue
                ? $"This phone is still on account {liveImeiAccount}. End that contract and remove the device first."
                : null;
        }

        // ---- Verification: is the phone really locked? ----------------------

        public record StockCheckResult(bool Locked, string Text);

        public static StockCheckResult KnoxCheck(DeviceDto? device, DateTime nowUtc)
        {
            if (device == null)
            {
                return new(false, "Not in Knox yet. Veritech usually passes it on within a few minutes.");
            }

            var seen = device.LastSeen > 0 ? $" Last seen {FromMillis(device.LastSeen):dd/MM/yyyy HH:mm} UTC." : " Not seen online yet.";
            if (!RecoveryRules.KnoxActiveStatuses.Contains(device.Status))
            {
                return new(false, $"In Knox as {device.Status}: switch the phone on and connect it to the internet.{seen}");
            }

            var relockDue = device.RelockTimestamp > 0 && FromMillis(device.RelockTimestamp) <= nowUtc;
            if (device.Status == "Locked" || (relockDue && device.LatestRelockApplied))
            {
                return new(true, $"Locked in Knox.{seen}");
            }

            return relockDue
                ? new(false, $"Active in Knox, but the lock hasn't reached the phone yet: connect it to the internet.{seen}")
                : new(false, $"Active in Knox but NOT locked. Use Approve & lock again.{seen}");
        }

        public static StockCheckResult TranssionCheck(FindLockStateResponse? response)
        {
            var d = response?.Data;
            if (response == null || !response.IsSuccess || d == null)
            {
                return new(false, $"Transsion doesn't have this phone: {response?.Message ?? "no answer"}.");
            }

            var seen = d.LastConnectTime is > 0 ? $" Last connected {FromMillis(d.LastConnectTime.Value):dd/MM/yyyy HH:mm} UTC." : "";
            if (d.LockState < 3000)
            {
                return new(false, $"Waiting at Transsion: switch the phone on and connect it to the internet. It locks as soon as it activates.{seen}");
            }

            return d.MobileStatus == 1000
                ? new(true, $"Locked by Transsion.{seen}")
                : new(false, $"Active at Transsion but NOT locked.{seen}");
        }

        // Transsion sends seconds in some fields and milliseconds in others.
        private static DateTime FromMillis(long value) =>
            (value < 100_000_000_000 ? DateTimeOffset.FromUnixTimeSeconds(value) : DateTimeOffset.FromUnixTimeMilliseconds(value)).UtcDateTime;

        public static string StatusText(Enrolment e)
        {
            if (!IsStock(e))
            {
                return "Sold";
            }

            if (e.StockVerifiedAt.HasValue)
            {
                return "Locked ✓";
            }

            if (DeviceLockRules.ProviderForBrand(e.DeviceBrand) == LockProvider.Knox && !e.StockKnoxApprovedAt.HasValue)
            {
                return "Waiting for Approve & lock";
            }

            return DeviceLockRules.IsTranssion(e.DeviceBrand) && e.ActivatedAt.HasValue
                ? "Switched on – check the lock"
                : "Not verified";
        }

        // ---- Sale ----------------------------------------------------------

        // The stock brand was chosen by an admin (and checked against the
        // TAC), so it stands unless the order names a different brand.
        public static string? CheckSaleBrand(Enrolment stock, string? orderBrand) =>
            orderBrand != null && !string.Equals(orderBrand, stock.DeviceBrand, StringComparison.OrdinalIgnoreCase)
                ? $"The order is for a {orderBrand} phone, but stock phone {stock.IMEI} is a {stock.DeviceBrand}. Check the IMEI and the order."
                : null;

        // The sale's details go onto the stock enrolment, which then waits
        // for approval like any other: Transsion for the approval call
        // (it may already be switched on), Knox for Approve.
        public static void AttachSale(Enrolment stock, Enrolment sale, DateTime nowUtc)
        {
            stock.AccountId = sale.AccountId;
            stock.OrderId = sale.OrderId;
            stock.FirstName = sale.FirstName;
            stock.LastName = sale.LastName;
            stock.DealerId = sale.DealerId;
            stock.EnrolledByUserId = sale.EnrolledByUserId;
            stock.EnrolledByRole = sale.EnrolledByRole;
            stock.ProductName = sale.ProductName;
            stock.DealerRef = sale.DealerRef;
            stock.RequiredDeposit = sale.RequiredDeposit;
            stock.DepositPaid = sale.DepositPaid;
            stock.DepositShort = sale.DepositShort;
            stock.UpdatedBy = sale.UpdatedBy;
            stock.Updated = nowUtc;
            stock.Status = DeviceLockRules.IsTranssion(stock.DeviceBrand)
                ? DeviceLockRules.WaitingStatus(stock)
                : EnrolmentStatus.Pending;
        }
    }
}
