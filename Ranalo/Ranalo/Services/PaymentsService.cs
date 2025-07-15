using Ranalo.Models;
using System.Data;

namespace Ranalo.Services
{
    public class PaymentsService
    {
        private readonly IApplicationReportService _reportsService;
        public PaymentsService(IApplicationReportService reportsService) 
        { 
            _reportsService = reportsService;
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
            var payments = await _reportsService.GetAllPaymentsAsync();
            var allOrphaned = await _reportsService.GetOrphanedPaymentsAsync();

            var orphaned = allOrphaned.DistinctBy(r => r.MpesaCode).ToList();
            //.DistinctBy(r => r.MpesaCode).ToList();
            var merged = from p in payments.Payments
                         join o in orphaned on p.MpesaCode equals o.MpesaCode into oo
                         select new { Payment = p, Orphan = oo.FirstOrDefault() };

            return DataTableConverter.ToDataTable(merged.ToList());
        }

       

    }
}
