using Ranalo.DataStore;
using Ranalo.Models;

namespace Ranalo.Services
{
    public interface IPaymentReminderService
    {
        Task<List<AccountSendMessage>> RunRemindersAsync(List<AccountSendMessage> records, IPaymentsRepository paymentsRepository);
    }
}