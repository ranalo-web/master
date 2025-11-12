
namespace Ranalo.DataStore.MySql
{
    public interface IMySqlPaymentsRepository
    {
        Task<object>? GetPaymentByIdAsync(int id);
    }
}