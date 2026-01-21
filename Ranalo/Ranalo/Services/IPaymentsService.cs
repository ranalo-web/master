using Ranalo.Models;
using Ranalo.Woocommece.Api.Models;
using System.Data;

namespace Ranalo.Services
{
    public interface IPaymentsService
    {
        Task<List<string>?> CreatePayments(List<MpesaRecord> payments);
        Task<DataTable> GetPaymentsStatusReport();
        List<MpesaRecord> MapXlsPayments(List<PaymentTransaction> payments);
        Task<DataTable> PaymentsWithOrphanedSummary();
    }
}