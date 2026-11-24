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
        Task<Dealer?> GetDealerByUserIdAsync(int userId);
        Task<Dealer?> GetDealerByDealerIdAsync(int dealerId);

        Task<User?> GetByCustomerIdAsync(int userId);
        Task<User?> GetAnyUserByUserIdAsync(int userId);
        Task<IEnumerable<User>> GetUsersByDealerIdAsync(int dealerId);
        Task<IEnumerable<User>> GetAllUsersAsync();

        // Paged variants backing the Users list page (Views/Home/Users.cshtml)
        // -- the unbounded methods above previously loaded every row with no
        // limit at all, rendered behind fake, non-functional pagination.
        Task<Ranalo.Models.UsersPage> GetUsersPageAsync(int? dealerId, bool suspended, UserRole? role, int page, int pageSize, string searchTerm);

        Task<User> CreateUserAsync(User newUser);
        Task<User> UpdateUserAsync(User newUser);
        Task<User?> GetUserByEmailAsync(string email);


        Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash);
        Task<User?> GetSignedInUserAsync(int userId);
        Task UpdateUserLastLogin(User user);
        Task StampLastSeenAsync(int userId, DateTime utc);
        Task<IEnumerable<User>> GetDebtCollectorsAsync();
        Task<IEnumerable<Dealer>> GetAllDealersAsync();
        Task<Dealer?> GetDealerByDealerRefAsync(string dealerReference);
        Task CreateDealerAsync(Dealer dealerDetails);
        Task<(Dealer Dealer, User Login)> CreateDealerWithLoginAsync(Dealer dealer, User login);

        Task<IEnumerable<User>> GetAgentsByDealerIdAsync(int dealerId);

        Task<IEnumerable<User>> GetAgentsAsync();
    }
}
