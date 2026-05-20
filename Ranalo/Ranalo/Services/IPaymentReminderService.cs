using Ranalo.DataStore;
using Ranalo.Models;
using Ranalo.SumsungKnox;
using Ranalo.SumsungKnox.Models;

namespace Ranalo.Services
{
    public interface IPaymentReminderService
    {
        Task<List<AccountSendMessage>> RunRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository);

        Task<List<AccountSendMessage>> RunPaymentsSummariesAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository);
        Task RunRemindersKnoxAsync(List<AccountSendMessage> knoxReminders, IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient);

        Task<List<AccountSendMessage>> RunKnoxPaymentsSummariesAsync(List<AccountSendMessage> records,
            IPaymentsRepository paymentsRepository, IKnoxGuardClient knoxGuardClient);
    }
}