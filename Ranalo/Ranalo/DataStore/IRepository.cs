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
        Task<Dealer?> GetDealerByDealerIdAsync(int dealerId);

        Task<User?> GetByCustomerIdAsync(int userId);
        Task<User?> GetAnyUserByUserIdAsync(int userId);
        Task<IEnumerable<User>> GetUsersByDealerIdAsync(int dealerId);
        Task<IEnumerable<User>> GetAllUsersAsync();

        // Paged variants backing the Users list page (Views/Home/Users.cshtml)
        // -- the unbounded methods above previously loaded every row with no
        // limit at all, rendered behind fake, non-functional pagination.
        Task<(List<User> Users, int TotalCount)> GetAllUsersPagedAsync(int page, int pageSize, string searchTerm);
        Task<(List<User> Users, int TotalCount)> GetUsersByDealerIdPagedAsync(int dealerId, int page, int pageSize, string searchTerm);

        Task<User> CreateUserAsync(User newUser);
        Task<User> UpdateUserAsync(User newUser);
        Task<User?> GetUserByEmailAsync(string email);

        Task<User?> GetUserByPasswordAsync(string password);

        Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash);
        Task UpdateUserLastLogin(User user);
        Task<IEnumerable<User>> GetDebtCollectorsAsync();
        Task<IEnumerable<Dealer>> GetAllDealersAsync();
        Task<Dealer?> GetDealerByDealerRefAsync(string dealerReference);
        Task CreateDealerAsync(Dealer dealerDetails);

        Task<IEnumerable<User>> GetAgentsByDealerIdAsync(int dealerId);

        Task<IEnumerable<User>> GetAgentsAsync();
    }
}
