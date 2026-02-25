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
            else
            {
                // Update existing user
                // You can copy properties from input 'user' to 'existingUser'
                existingUser.Name = user.Name;
                existingUser.RoleId = user.RoleId;
                existingUser.OtherSelectedRoles = user.OtherSelectedRoles;
                existingUser.Email = user.Email;
                existingUser.City = user.City;
                // ... copy any other properties you need

                await _userRepository.UpdateUserAsync(existingUser);
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


        public async Task<List<Dealer>?> GetAllDealers()
        {
            var dealers = await _userRepository.GetAllDealersAsync();

            return dealers.ToList();
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

        public async Task<IEnumerable<User>> GetDebtCollectors()
        {
            return await _userRepository.GetDebtCollectorsAsync();
        }

        public async Task AddDealerAsync(Dealer dealerDetails)
        {
            var existingDealer = await GetDealerByDealerRef(dealerDetails.DealerReference);
            if (existingDealer == null)
            {
                await _userRepository.CreateDealerAsync(dealerDetails);
            }

            return;
        }

        private async Task<Dealer?> GetDealerByDealerRef(string dealerReference)
        {
            return await _userRepository.GetDealerByDealerRefAsync(dealerReference);
        }
        // similarly: GetById, Update, Delete
    }
}
