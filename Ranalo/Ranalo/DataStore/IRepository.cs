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
    }
}
