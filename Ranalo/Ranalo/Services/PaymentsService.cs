using Ranalo.Models;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Models;
using System.Data;
using System.Globalization;

namespace Ranalo.Services
{
    public class PaymentsService : IPaymentsService
    {
        private readonly IApplicationReportService _reportsService;
        private readonly IKosePaymentsRepository _kosePaymentsRepo;
        public PaymentsService(IApplicationReportService reportsService, IKosePaymentsRepository kosePaymentsRepo)
        {
            _reportsService = reportsService;
            _kosePaymentsRepo = kosePaymentsRepo;
        }


        public async Task<DataTable> GetPaymentsStatusReport()
        {

            var allPayments = await PaymentsWithOrphanedSummary();

            var devices = await _reportsService.GetAllDevicesAsync();

            var devicesToTable = DataTableConverter.ToDataTable(devices);

            var contractInfo = await _reportsService.GetAllOrdersAsync();
            var contractInfoToTable = DataTableConverter.ToDataTable(contractInfo);

            var dealerInfo = await _reportsService.GetAllDealersAsync();
            var dealerInfoToTable = DataTableConverter.ToDataTable(dealerInfo);

            var pTable1 = DataTableConverter.GetPTable1(allPayments);
            var pTable2 = DataTableConverter.GetPTable2(allPayments);
            var ptable3 = DataTableConverter.GetPTable3(allPayments);
            var ptable4 = DataTableConverter.GetPTable4(allPayments, ptable3);
            var ptable5 = DataTableConverter.GetPTable5(allPayments, pTable2);

            var paymentsProcessor = new PaymentSummaryProcessor
                (
                devicesToTable,
                pTable1,
                pTable2,
                ptable3,
                ptable4,
                ptable5,
                contractInfoToTable,
                dealerInfoToTable
                );


            var fullyPaidIds = new List<int>();
            var result = paymentsProcessor.BuildSummary(fullyPaidIds);

            return new DataTable();
        }
        public async Task<DataTable> PaymentsWithOrphanedSummary()
        {
            var payments = await _reportsService.GetAllPaymentsAsync(null);
            var allOrphaned = await _reportsService.GetOrphanedPaymentsAsync(1, 1000);

            var orphaned = allOrphaned.Payments?.DistinctBy(r => r.MpesaCode).ToList();
            //.DistinctBy(r => r.MpesaCode).ToList();
            var merged = from p in payments.Payments
                         join o in orphaned on p.MpesaCode equals o.MpesaCode into oo
                         select new { Payment = p, Orphan = oo.FirstOrDefault() };

            return DataTableConverter.ToDataTable(merged.ToList());
        }

        public async Task<List<string>?> CreatePayments(List<MpesaRecord> payments)
        {
            Dictionary<string, List<MpesaRecord>>? grouped = new Dictionary<string, List<MpesaRecord>>();

            grouped = payments
                .GroupBy(r => r.AccountNo)
                .ToDictionary(g => g.Key, g => g.ToList());

            return await _kosePaymentsRepo.SaveToDatabaseAsync(grouped);
        }

        public List<MpesaRecord> MapXlsPayments(List<PaymentTransaction> payments)
        {

            var results = new List<MpesaRecord>();

            foreach (var payment in payments)
            {
                results.Add(new MpesaRecord
                {
                    AccountNo = payment.AccountNumber,
                    Amount = payment.PaidIn.ToString(),
                    MpesaCode = payment.ReceiptNo,
                    PaymentDate = ParseExcelDateToAmPmString(payment.CompletionTime),
                    Imported = true
                });
            }

            return results;

        }

        public static string ParseExcelDateToAmPmString(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate))
                return null;

            var normalized = rawDate
                .Trim()
                .Replace('\u00A0', ' ')   // non-breaking space from Excel
                .Replace("–", "-")
                .Replace("—", "-");

            var formats = new[]
            {
        "dd-MM-yyyy HH:mm:ss",
        "d-M-yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        "M/d/yyyy h:mm:ss tt",   // fallback if already formatted
        "M/d/yyyy hh:mm:ss tt"
    };

            if (!DateTime.TryParseExact(
                    normalized,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                throw new FormatException($"Invalid date value: '{rawDate}'");
            }

            // Required output format: 1/5/2026 8:07:28 PM
            return date.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
        }

    }
}
