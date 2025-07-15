using Ranalo.DataStore.DataModels;
using Ranalo.DataStore;

namespace Ranalo.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository _userRepository;

        public UserService(IRepository userRepository)
        {
            _userRepository = userRepository;
        }

        //public async Task<IEnumerable<User>> GetAllUsersAsync()
        //{
        //    //return await _userRepository.GetAllAsync();
        //}

        //public async Task AddUserAsync(User user)
        //{
        //    await _userRepository.AddAsync(user);
        //    await _userRepository.SaveAsync();
        //}

        public async Task<User?> LoginUser(string email, string password)
        {
            return await _userRepository.GetByEmailAndPasswordAsync(email, password);
        }

        public async Task<Dealer?> GetDealerByUserId(int userId)
        {
            return await _userRepository.GetDealerByUserIdAsync(userId);
        }

        public async Task<User?> GetUserByCustomerIdAsync(int userId)
        {
            return await _userRepository.GetByCustomerIdAsync(userId);
        }

        // similarly: GetById, Update, Delete
    }
}
