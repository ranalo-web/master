using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    public interface IUserService
    {
        //Task AddUserAsync(User user);
        //Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> LoginUser(string email, string password);

        Task<Dealer?> GetDealerByUserId(int userId);

        Task<User?> GetUserByCustomerIdAsync(int userId);
    }
}