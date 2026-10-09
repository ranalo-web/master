using System.Globalization;
using System.Text.RegularExpressions;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Fraud early-warning checks (admin only). Kept free of I/O so they can
    // be unit tested; FraudRepository loads the rows, FraudService joins in
    // the admin's reviews.
    //
    // Built from the Newway Electrical case (2026-10-09): a dealer sold
    // phones to ghost customers to collect commission -- deposit paid and
    // nothing after, contracts with no device, the same phone enrolled on
    // another account, one phone number behind several ID numbers.
    //
    // Account checks (open contracts)
    //   Deposit, no payment   14+ days old and paid no more than the deposit.
    //                         Also reported per dealer against the company
    //                         rate, which is the strongest signal.
    //   Contract, no device   an open contract with no enrolled device.
    //   Lock overdue          lock date passed, device still unlocked, and
    //                         the customer is behind.
    // Order checks (WooCommerce orders not cancelled/failed/rejected)
    //   IMEI on another account  the order's IMEI is on a device that already
    //                         has an open contract for a different customer.
    //   IMEI / phone / M-Pesa deposit code shared by different ID numbers.
    //   Next of kin phone or ID number shared by 3+ different customers.
    public static class FraudRules
    {
        public const int DepositOnlyMinDays = 14;
        public const decimal DepositOnlyTolerance = 1.05m;   // a few shillings over the deposit still counts
        public const int NextOfKinMinCustomers = 3;
        public const int NewFlagDays = 7;

        public static readonly IReadOnlyList<FraudCheckInfo> Checks = new[]
        {
            new FraudCheckInfo(FraudCheckCodes.DepositOnly, "Deposit, no payment",
                $"Contract is {DepositOnlyMinDays}+ days old and nothing has been paid beyond the deposit.", true),
            new FraudCheckInfo(FraudCheckCodes.NoDevice, "Contract, no device",
                "Open contract with no device enrolled on the account.", true),
            new FraudCheckInfo(FraudCheckCodes.ImeiOnOtherAccount, "IMEI on another account",
                "The order's IMEI is on a device that already has an open contract for a different customer.", true),
            new FraudCheckInfo(FraudCheckCodes.ImeiReused, "IMEI on several IDs",
                "The same IMEI is on orders with different ID numbers.", true),
            new FraudCheckInfo(FraudCheckCodes.PhoneMultiId, "Phone on several IDs",
                "One customer phone number is on orders with different ID numbers.", true),
            new FraudCheckInfo(FraudCheckCodes.DepositRefReused, "Deposit code reused",
                "The same M-Pesa deposit code is on orders with different ID numbers.", true),
            new FraudCheckInfo(FraudCheckCodes.NextOfKinShared, "Shared next of kin",
                $"The same next-of-kin phone or ID number is given by {NextOfKinMinCustomers}+ different customers.", false),
            new FraudCheckInfo(FraudCheckCodes.LockOverdue, "Lock overdue, unlocked",
                "Lock date has passed but the device is still unlocked and the customer is behind.", false),
        };

        public static FraudCheckInfo? GetCheck(string? code) => Checks.FirstOrDefault(c => c.Code == code);

        // --- Normalising what customers and dealers type ---------------------

        // Kenyan mobile as its 9 significant digits (7xxxxxxxx / 1xxxxxxxx),
        // whether typed 07.., 254.., +254.. or with spaces; null if not one.
        public static string? NormalizePhone(string? phone)
        {
            var digits = DigitsOnly(phone);
            if (digits.StartsWith("254")) digits = digits[3..];
            else if (digits.StartsWith("0")) digits = digits[1..];
            return digits.Length == 9 && (digits[0] == '7' || digits[0] == '1') ? digits : null;
        }

        public static string? NormalizeImei(string? imei)
        {
            var digits = DigitsOnly(imei);
            return digits.Length == 15 ? digits : null;
        }

        // Kenyan ID numbers are 6-9 digits; leading zeros don't matter.
        public static string? NormalizeNationalId(string? id)
        {
            var trimmed = (id ?? "").Trim();
            if (!Regex.IsMatch(trimmed, @"^\d{5,10}$")) return null;
            var value = trimmed.TrimStart('0');
            return value.Length >= 5 ? value : null;
        }

        // M-Pesa codes are 10 letters and digits (e.g. SEO286BS6I).
        public static string? NormalizeMpesaRef(string? code)
        {
            var value = (code ?? "").Trim().ToUpperInvariant();
            return Regex.IsMatch(value, @"^(?=.*\d)(?=.*[A-Z])[A-Z0-9]{10}$") ? value : null;
        }

        private static string DigitsOnly(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());

        public static DateTime? ParseLockDate(string? value) =>
            DateTime.TryParseExact((value ?? "").Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date : null;

        // A customer's account is their ID number, or the ID with one digit
        // 1-9 appended for a second phone.
        public static bool IsAccountOfId(long accountNo, string? nationalId)
        {
            if (nationalId == null || !long.TryParse(nationalId, out var id)) return false;
            return accountNo == id || (accountNo / 10 == id && accountNo % 10 != 0);
        }

        // --- Checks ----------------------------------------------------------

        public static List<FraudFlag> Evaluate(
            IEnumerable<FraudContractRow> contracts,
            IEnumerable<FraudOrderRow> orders,
            IEnumerable<FraudNextOfKinRow> nextOfKin,
            IEnumerable<FraudDeviceRow> devices,
            DateTime today)
        {
            var flags = new List<FraudFlag>();
            var orderList = orders.ToList();
            flags.AddRange(AccountChecks(contracts, today.Date));
            flags.AddRange(ImeiOnOtherAccount(orderList, devices));
            flags.AddRange(SharedDetail(FraudCheckCodes.ImeiReused, "IMEI", orderList, o => NormalizeImei(o.Imei)));
            flags.AddRange(SharedDetail(FraudCheckCodes.PhoneMultiId, "Phone", orderList, o => NormalizePhone(o.Phone)));
            flags.AddRange(SharedDetail(FraudCheckCodes.DepositRefReused, "Deposit code", orderList, o => NormalizeMpesaRef(o.MpesaDepositRef)));
            flags.AddRange(SharedNextOfKin(orderList, nextOfKin));
            return flags;
        }

        private static IEnumerable<FraudFlag> AccountChecks(IEnumerable<FraudContractRow> contracts, DateTime today)
        {
            foreach (var c in contracts)
            {
                var atRisk = Math.Max(0m, c.ContractValue - c.TotalPaid);
                FraudFlag Flag(string code, string evidence, DateTime flaggedOn) => new()
                {
                    CheckCode = code,
                    SubjectKey = c.AccountNo.ToString(CultureInfo.InvariantCulture),
                    AccountNo = c.AccountNo,
                    CustomerName = c.CustomerName,
                    DealerId = c.DealerId,
                    DealerName = c.DealerName,
                    Evidence = evidence,
                    FlaggedOn = flaggedOn,
                    AmountAtRisk = atRisk,
                };

                var age = (today - c.StartDate.Date).Days;
                if (IsDepositOnly(c, today))
                {
                    yield return Flag(FraudCheckCodes.DepositOnly,
                        $"Started {c.StartDate:dd/MM/yyyy} ({age} days). Deposit KES {c.Deposit:N0}, paid KES {c.TotalPaid:N0} in total"
                        + (c.LastPaymentDate.HasValue ? $", last payment {c.LastPaymentDate:dd/MM/yyyy}." : "."),
                        c.StartDate.Date.AddDays(DepositOnlyMinDays));
                }

                if (!c.HasDevice)
                {
                    yield return Flag(FraudCheckCodes.NoDevice,
                        $"Started {c.StartDate:dd/MM/yyyy}, no device enrolled on account {c.AccountNo}. Paid KES {c.TotalPaid:N0}.",
                        c.StartDate.Date);
                }

                var lockDate = ParseLockDate(c.NextLockDate);
                if (c.HasDevice && c.Locked == false && lockDate.HasValue && lockDate.Value < today && c.Shortfall > 0)
                {
                    yield return Flag(FraudCheckCodes.LockOverdue,
                        $"{c.DeviceName}: lock date {lockDate:dd/MM/yyyy} passed, still unlocked. KES {c.Shortfall:N0} behind"
                        + (string.IsNullOrWhiteSpace(c.LastConnectedAt) ? "." : $", last connected {c.LastConnectedAt}."),
                        lockDate.Value);
                }
            }
        }

        public static bool IsDepositOnly(FraudContractRow c, DateTime today) =>
            (today.Date - c.StartDate.Date).Days >= DepositOnlyMinDays
            && c.Deposit > 0
            && c.TotalPaid <= c.Deposit * DepositOnlyTolerance;

        private static IEnumerable<FraudFlag> ImeiOnOtherAccount(List<FraudOrderRow> orders, IEnumerable<FraudDeviceRow> devices)
        {
            var byImei = new Dictionary<string, FraudDeviceRow>();
            foreach (var d in devices)
            {
                foreach (var imei in new[] { NormalizeImei(d.Imei), NormalizeImei(d.Imei2) })
                {
                    if (imei != null) byImei.TryAdd(imei, d);
                }
            }

            foreach (var o in orders)
            {
                var imei = NormalizeImei(o.Imei);
                if (imei == null || !byImei.TryGetValue(imei, out var device)) continue;

                var nid = NormalizeNationalId(o.NationalId);
                // The order's own phone, or the same customer's earlier order.
                if (o.AccountNo == device.AccountNo
                    || IsAccountOfId(device.AccountNo, nid)
                    || (nid != null && NormalizeNationalId(device.ContractOrderNationalId) == nid))
                {
                    continue;
                }

                yield return new FraudFlag
                {
                    CheckCode = FraudCheckCodes.ImeiOnOtherAccount,
                    SubjectKey = "order:" + o.OrderId.ToString(CultureInfo.InvariantCulture),
                    AccountNo = o.AccountNo,
                    OrderIds = new List<long> { o.OrderId },
                    CustomerName = o.CustomerName,
                    DealerId = o.DealerId,
                    DealerName = o.DealerName ?? o.DealerRef,
                    Evidence = $"Order {o.OrderId} ({o.Status}, ID {o.NationalId}): IMEI {imei} is on account {device.AccountNo}"
                        + $" ({device.CustomerName}), which already has an open contract.",
                    FlaggedOn = o.DateCreated,
                };
            }
        }

        // Orders sharing one detail (IMEI, phone, deposit code) across
        // different ID numbers. Orders with no usable ID count as their own.
        private static IEnumerable<FraudFlag> SharedDetail(string code, string label, List<FraudOrderRow> orders, Func<FraudOrderRow, string?> key)
        {
            foreach (var group in orders.Select(o => (Key: key(o), Order: o)).Where(x => x.Key != null).GroupBy(x => x.Key!))
            {
                var groupOrders = group.Select(x => x.Order).OrderBy(o => o.DateCreated).ToList();
                var ids = groupOrders.Select(CustomerKey).Distinct().ToList();
                if (ids.Count < 2) continue;

                yield return GroupFlag(code, group.Key,
                    $"{label} {group.Key} is on {groupOrders.Count} orders with {ids.Count} different ID numbers: "
                    + string.Join("; ", groupOrders.Select(Describe)) + ".",
                    groupOrders);
            }
        }

        private static IEnumerable<FraudFlag> SharedNextOfKin(List<FraudOrderRow> orders, IEnumerable<FraudNextOfKinRow> nextOfKin)
        {
            var ordersById = orders.GroupBy(o => o.OrderId).ToDictionary(g => g.Key, g => g.First());
            var keyed = new List<(string Key, string Label, FraudOrderRow Order)>();
            foreach (var n in nextOfKin)
            {
                if (!ordersById.TryGetValue(n.OrderId, out var order)) continue;
                var phone = NormalizePhone(n.Phone);
                if (phone != null) keyed.Add(("phone:" + phone, "phone " + phone, order));
                var id = NormalizeNationalId(n.IdNumber);
                if (id != null) keyed.Add(("id:" + id, "ID number " + id, order));
            }

            foreach (var group in keyed.GroupBy(x => x.Key))
            {
                var groupOrders = group.Select(x => x.Order).DistinctBy(o => o.OrderId).OrderBy(o => o.DateCreated).ToList();
                var customers = groupOrders.Select(CustomerKey).Distinct().Count();
                if (customers < NextOfKinMinCustomers) continue;

                yield return GroupFlag(FraudCheckCodes.NextOfKinShared, group.Key,
                    $"Next of kin {group.First().Label} is given by {customers} different customers: "
                    + string.Join("; ", groupOrders.Select(Describe)) + ".",
                    groupOrders);
            }
        }

        private static string CustomerKey(FraudOrderRow o) =>
            NormalizeNationalId(o.NationalId) ?? "order:" + o.OrderId.ToString(CultureInfo.InvariantCulture);

        private static string Describe(FraudOrderRow o) =>
            $"order {o.OrderId} {o.CustomerName} (ID {o.NationalId}, {o.Status})";

        private static FraudFlag GroupFlag(string code, string subject, string evidence, List<FraudOrderRow> groupOrders)
        {
            var dealerIds = groupOrders.Where(o => o.DealerId.HasValue).Select(o => o.DealerId!.Value).Distinct().ToList();
            var dealerNames = groupOrders.Select(o => o.DealerName ?? o.DealerRef).Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var accounts = groupOrders.Where(o => o.AccountNo.HasValue).Select(o => o.AccountNo!.Value).Distinct().ToList();

            return new FraudFlag
            {
                CheckCode = code,
                SubjectKey = subject,
                AccountNo = accounts.Count == 1 ? accounts[0] : null,
                OrderIds = groupOrders.Select(o => o.OrderId).ToList(),
                CustomerName = string.Join(", ", groupOrders.Select(o => o.CustomerName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()),
                DealerId = dealerIds.Count == 1 ? dealerIds[0] : null,
                DealerName = dealerNames.Count == 0 ? null : string.Join(" / ", dealerNames),
                Evidence = evidence,
                FlaggedOn = groupOrders.Max(o => o.DateCreated),
            };
        }

        // --- Reports ---------------------------------------------------------

        public static decimal DepositOnlyRate(IEnumerable<FraudContractRow> contracts, DateTime today)
        {
            var aged = contracts.Where(c => (today.Date - c.StartDate.Date).Days >= DepositOnlyMinDays).ToList();
            return aged.Count == 0 ? 0m : (decimal)aged.Count(c => IsDepositOnly(c, today)) / aged.Count;
        }

        // High: confirmed fraud, or at least 5 deposit-only contracts (of 10+)
        // at twice the company rate. Watch: 3+ deposit-only at 1.5x the rate,
        // or 3+ other open flags.
        public static FraudRiskLevel RiskLevel(FraudDealerRisk d, decimal overallRate)
        {
            if (d.Confirmed > 0) return FraudRiskLevel.High;
            if (d.Contracts >= 10 && d.DepositOnly >= 5 && d.DepositOnlyRate >= 2 * overallRate) return FraudRiskLevel.High;
            if (d.DepositOnly >= 3 && d.DepositOnlyRate >= 1.5m * overallRate) return FraudRiskLevel.Watch;
            if (d.OtherFlags >= 3) return FraudRiskLevel.Watch;
            return FraudRiskLevel.Normal;
        }

        // Per-dealer report. Cleared flags don't count.
        public static List<FraudDealerRisk> DealerRisks(IEnumerable<FraudContractRow> contracts, IEnumerable<FraudFlag> flags, DateTime today)
        {
            var contractList = contracts.ToList();
            var overall = DepositOnlyRate(contractList, today);
            var dealers = new Dictionary<string, FraudDealerRisk>(StringComparer.OrdinalIgnoreCase);

            FraudDealerRisk For(int? dealerId, string? name)
            {
                var key = dealerId?.ToString(CultureInfo.InvariantCulture) ?? "name:" + (string.IsNullOrWhiteSpace(name) ? "" : name.Trim());
                if (!dealers.TryGetValue(key, out var d))
                {
                    d = new FraudDealerRisk { DealerId = dealerId, DealerName = string.IsNullOrWhiteSpace(name) ? "(no dealer)" : name.Trim() };
                    dealers[key] = d;
                }
                return d;
            }

            foreach (var c in contractList.Where(c => (today.Date - c.StartDate.Date).Days >= DepositOnlyMinDays))
            {
                For(c.DealerId, c.DealerName).Contracts++;
            }

            foreach (var group in flags.Where(f => f.ReviewStatus != FraudReviewStatus.Cleared)
                         // Order flags count only when the dealer is known from
                         // the device; the dealer typed on the order is free text.
                         .Where(f => f.DealerId.HasValue || IsAccountCheck(f.CheckCode))
                         .GroupBy(f => (f.DealerId, Name: f.DealerId.HasValue ? null : f.DealerName?.Trim().ToUpperInvariant())))
            {
                var d = For(group.Key.DealerId, group.First().DealerName);
                foreach (var f in group)
                {
                    if (f.CheckCode == FraudCheckCodes.DepositOnly) d.DepositOnly++;
                    else if (f.CheckCode == FraudCheckCodes.LockOverdue) d.LockOverdue++;
                    else d.OtherFlags++;
                    if (f.ReviewStatus == FraudReviewStatus.Confirmed) d.Confirmed++;
                }
                // Money at risk once per account, not once per flag.
                d.AmountAtRisk += group.Where(f => f.AccountNo.HasValue && f.CheckCode != FraudCheckCodes.LockOverdue)
                    .GroupBy(f => f.AccountNo).Sum(g => g.Max(f => f.AmountAtRisk));
            }

            foreach (var d in dealers.Values)
            {
                d.Level = RiskLevel(d, overall);
            }

            return dealers.Values
                .OrderByDescending(d => d.Level)
                .ThenByDescending(d => d.DepositOnlyRate)
                .ThenBy(d => d.DealerName)
                .ToList();
        }

        // --- Dealer / agent view ---------------------------------------------
        //
        // Dealers and agents see an account or order as "Under verification"
        // as soon as any check fires on it, and "Failed – suspected fraud"
        // once an admin confirms. Never which check, nor the evidence.
        // Cleared flags and lock-overdue (our own lock problem, not the
        // dealer's doing) never show.
        public static List<VerificationItem> VerificationItems(
            IEnumerable<FraudFlag> flags, IEnumerable<FraudContractRow> contracts, IEnumerable<FraudOrderRow> orders)
        {
            var contractsByAccount = contracts.GroupBy(c => c.AccountNo).ToDictionary(g => g.Key, g => g.First());
            var ordersById = orders.GroupBy(o => o.OrderId).ToDictionary(g => g.Key, g => g.First());
            var items = new Dictionary<string, VerificationItem>();

            void Add(long? accountNo, long? orderId, string? customer, int? dealerId, long? agentUserId, DateTime since, bool confirmed)
            {
                var key = accountNo.HasValue ? "a:" + accountNo : "o:" + orderId;
                if (!items.TryGetValue(key, out var item))
                {
                    item = new VerificationItem
                    {
                        AccountNo = accountNo, OrderId = orderId, CustomerName = customer,
                        DealerId = dealerId, AgentUserId = agentUserId, Since = since,
                    };
                    items[key] = item;
                }
                item.OrderId ??= orderId;
                item.DealerId ??= dealerId;
                item.AgentUserId ??= agentUserId;
                if (since < item.Since) item.Since = since;
                if (confirmed) item.Status = VerificationStatus.Failed;
            }

            foreach (var f in flags)
            {
                if (f.CheckCode == FraudCheckCodes.LockOverdue || f.ReviewStatus == FraudReviewStatus.Cleared) continue;
                var confirmed = f.ReviewStatus == FraudReviewStatus.Confirmed;

                if (IsAccountCheck(f.CheckCode) && f.AccountNo.HasValue)
                {
                    var c = contractsByAccount.GetValueOrDefault(f.AccountNo.Value);
                    Add(f.AccountNo, null, f.CustomerName, f.DealerId, c?.AgentUserId, f.FlaggedOn, confirmed);
                    continue;
                }

                // Order checks: each order on its own, with its own dealer.
                foreach (var orderId in f.OrderIds)
                {
                    if (!ordersById.TryGetValue(orderId, out var o)) continue;
                    Add(o.AccountNo, o.OrderId, o.CustomerName, o.DealerId, o.AgentUserId, f.FlaggedOn, confirmed);
                }
            }

            return items.Values.OrderByDescending(i => i.Status == VerificationStatus.Failed).ThenByDescending(i => i.Since).ToList();
        }

        public static bool IsAccountCheck(string code) =>
            code is FraudCheckCodes.DepositOnly or FraudCheckCodes.NoDevice or FraudCheckCodes.LockOverdue;

        public static List<FraudCheckSummary> Summaries(IEnumerable<FraudFlag> flags, DateTime today)
        {
            var since = today.Date.AddDays(-NewFlagDays);
            var active = flags.Where(f => f.ReviewStatus != FraudReviewStatus.Cleared).ToList();
            return Checks.Select(check =>
            {
                var mine = active.Where(f => f.CheckCode == check.Code).ToList();
                return new FraudCheckSummary
                {
                    Check = check,
                    Open = mine.Count,
                    NewThisWeek = mine.Count(f => f.FlaggedOn >= since),
                    Confirmed = mine.Count(f => f.ReviewStatus == FraudReviewStatus.Confirmed),
                    AmountAtRisk = mine.Sum(f => f.AmountAtRisk),
                };
            }).ToList();
        }
    }
}
