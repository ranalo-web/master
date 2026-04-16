using Ranalo.DataStore;
using Ranalo.DataStore.DataModels;

namespace Ranalo.Services
{
    public interface IUserService
    {
        //Task AddUserAsync(User user);
        //Task<IEnumerable<User>> GetAllUsersAsync();

        Task SuspendUserAsync(int userId);
        Task<User?> LoginUser(string email, string password);

        Task<Dealer?> GetDealerByUserId(int userId);

        Task<User?> GetUserByCustomerIdAsync(int userId);

        Task<User?> GetUserAnyUserByIdAsync(int userId);

        Task<List<User>> GetUsersByDealerIdAsync(int dealerId);

        Task<List<User>> GetAllUsersAsync();

        Task AddUserAsync(User user);

        Task<User?> GetUserByPasswordAsync(string password);

        Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash);
        Task<IEnumerable<User>> GetDebtCollectors();

        Task<List<Dealer>?> GetAllDealers();
        Task AddDealerAsync(Dealer dealerDetails);
    }
}