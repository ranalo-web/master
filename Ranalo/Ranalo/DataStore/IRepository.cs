using Ranalo.DataStore.DataModels;

namespace Ranalo.DataStore
{
    public interface IRepository
    {
        //Task<IEnumerable<T>> GetAllAsync();
        //Task<T> GetByIdAsync(int id);
        //Task AddAsync(T entity);
        //void Update(T entity);
        //void Delete(T entity);
        Task SaveAsync();
        Task<User?> GetByEmailAndPasswordAsync(string email, string password);
        Task<Dealer?> GetDealerByUserIdAsync(int userId);

        Task<User?> GetByCustomerIdAsync(int userId);
        Task<IEnumerable<User>> GetUsersByDealerIdAsync(int dealerId);
        Task<IEnumerable<User>> GetAllUsersAsync();

        Task<User> CreateUserAsync(User newUser);
        Task<User?> GetUserByEmailAsync(string email);

        Task<User?> GetUserByPasswordAsync(string password);

        Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash);
        Task UpdateUserLastLogin(User user);
        Task<IEnumerable<User>> GetDebtCollectorsAsync();
    }
}
