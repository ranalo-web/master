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

        public async Task AddUserAsync(User user)
        {
            var existingUser = await GetUserByEmail(user.Email);
            if(existingUser == null)
            {
                await _userRepository.CreateUserAsync(user);
            }

            return;
        }

        public async Task<User?> LoginUser(string email, string password)
        {
            var user = await _userRepository.GetByEmailAndPasswordAsync(email, password);
            if(user != null)
            {
                await _userRepository.UpdateUserLastLogin(user);
            }

            return user;
        }

        public async Task<User?> GetUserByPasswordAsync(string password)
        {
            return await _userRepository.GetUserByPasswordAsync(password);
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }

        public async Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash)
        {
            return await _userRepository.UpdateUserPasswordAsync(userId, newPasswordHash);
        }

        public async Task<Dealer?> GetDealerByUserId(int userId)
        {
            return await _userRepository.GetDealerByUserIdAsync(userId);
        }

        public async Task<User?> GetUserByCustomerIdAsync(int userId)
        {
            return await _userRepository.GetByCustomerIdAsync(userId);
        }

        public async Task<List<User>> GetUsersByDealerIdAsync(int dealerId)
        {
            var users = await _userRepository.GetUsersByDealerIdAsync(dealerId);

            return users.ToList();
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllUsersAsync();

            return users.ToList();
        }
        // similarly: GetById, Update, Delete
    }
}
