using Ranalo.DataStore.DataModels;
using Ranalo.DataStore;

namespace Ranalo.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository _userRepository;
        private readonly ILogger<UserService>? _logger;

        // Passwords stored as typed are hashed when the person logs in -- but
        // not from a developer's machine: the local server uses the live
        // database, and the live site can't read a hash until this is deployed.
        private readonly bool _upgradePasswordsOnLogin;

        public UserService(IRepository userRepository, IHostEnvironment? environment = null, ILogger<UserService>? logger = null)
        {
            _userRepository = userRepository;
            _logger = logger;
            _upgradePasswordsOnLogin = environment?.IsDevelopment() != true;
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
                user.PasswordHash = PasswordRules.Hash(user.PasswordHash);
                await _userRepository.CreateUserAsync(user);
            }

            return;
        }

        public async Task UpdateUserAsync(User user)
        {
            var existingUser = await GetUserByEmail(user.Email);
            if (existingUser != null)
            {
                // Update existing user
                // You can copy properties from input 'user' to 'existingUser'
                existingUser.Name = user.Name;
                existingUser.RoleId = user.RoleId;
                existingUser.OtherSelectedRoles = user.OtherSelectedRoles;
                existingUser.Email = user.Email;
                existingUser.City = user.City;

                // Dealer: only changed when one is chosen (> 0); otherwise the
                // user keeps the dealer they already belong to.
                if (user.DealerId > 0)
                {
                    existingUser.DealerId = user.DealerId;
                    existingUser.ParentUserId = user.DealerId;
                }

                await _userRepository.UpdateUserAsync(existingUser);
            }

            return;
        }

        public async Task SuspendUserAsync(int userId)
        {
            var existingUser = await _userRepository.GetByCustomerIdAsync(userId);
            if (existingUser == null)
            {
                return;
            }
            else
            {
                // Update existing user
                // You can copy properties from input 'user' to 'existingUser'
                existingUser.Status = UserStatus.Suspended;
                // ... copy any other properties you need

                await _userRepository.UpdateUserAsync(existingUser);
            }

            return;
        }

        // null when the email and password don't match an active account.
        public async Task<User?> LoginUser(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            var user = await _userRepository.GetUserByEmailAsync(email.Trim());
            if (user == null || user.Status == UserStatus.Suspended)
            {
                return null;
            }

            var (ok, needsRehash) = PasswordRules.Verify(user.PasswordHash, password);
            if (!ok)
            {
                return null;
            }

            if (needsRehash && _upgradePasswordsOnLogin)
            {
                try
                {
                    user = await _userRepository.UpdateUserPasswordAsync(user.UserId, PasswordRules.Hash(password));
                }
                catch (Exception ex)
                {
                    // Still signed in; it's hashed next time.
                    _logger?.LogWarning(ex, "Couldn't hash the password for user {UserId}", user.UserId);
                }
            }

            await _userRepository.UpdateUserLastLogin(user);
            return user;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }

        public Task<User?> GetSignedInUserAsync(int userId) => _userRepository.GetSignedInUserAsync(userId);

        // A user loaded with GetUserAnyUserByIdAsync and changed.
        public Task SaveUserAsync(User user) => _userRepository.UpdateUserAsync(user);

        // newPassword as typed; it's stored hashed.
        public async Task<User> UpdateUserPasswordAsync(int userId, string newPassword)
        {
            return await _userRepository.UpdateUserPasswordAsync(userId, PasswordRules.Hash(newPassword));
        }

        public async Task<Dealer?> GetDealerByUserId(int userId)
        {
            return await _userRepository.GetDealerByUserIdAsync(userId);
        }

        // For a non-Dealer user (Agent, Collector) -- see
        // IRepository.GetDealerByDealerIdAsync's doc comment.
        public async Task<Dealer?> GetDealerByDealerId(int dealerId)
        {
            return await _userRepository.GetDealerByDealerIdAsync(dealerId);
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

        public async Task<User?> GetUserAnyUserByIdAsync(int userId)
        {
            return await _userRepository.GetAnyUserByUserIdAsync(userId);
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

        public async Task<(List<User> Users, int TotalCount)> GetAllUsersPagedAsync(int page, int pageSize, string searchTerm)
        {
            return await _userRepository.GetAllUsersPagedAsync(page, pageSize, searchTerm);
        }

        public async Task<(List<User> Users, int TotalCount)> GetUsersByDealerIdPagedAsync(int dealerId, int page, int pageSize, string searchTerm)
        {
            return await _userRepository.GetUsersByDealerIdPagedAsync(dealerId, page, pageSize, searchTerm);
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

        // Next dealer reference in sequence: one above the highest numeric
        // reference in use. Only a suggestion -- a dealer whose phones are
        // locked through Kose must use their Kose device group number.
        public async Task<string> SuggestDealerReferenceAsync()
        {
            var dealers = await _userRepository.GetAllDealersAsync();
            var highest = dealers
                .Select(d => int.TryParse(d.DealerReference, out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return (highest + 1).ToString();
        }

        // Add Dealer: validates, then creates the dealer's login and the
        // dealer together, linked both ways (see
        // Repository.CreateDealerWithLoginAsync).
        public async Task<(bool Ok, string Message)> AddDealerWithLoginAsync(Dealer dealer, User login)
        {
            dealer.CompanyName = dealer.CompanyName?.Trim() ?? "";
            dealer.DealerReference = dealer.DealerReference?.Trim() ?? "";
            login.Email = login.Email?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(dealer.CompanyName))
            {
                return (false, "Enter the dealer's name.");
            }
            if (!dealer.DealerReference.All(char.IsDigit) || dealer.DealerReference.Length == 0)
            {
                return (false, "The dealer reference must be a number (their Kose device group number, or the suggested next number).");
            }
            var sameReference = await GetDealerByDealerRef(dealer.DealerReference);
            if (sameReference != null)
            {
                return (false, $"Dealer reference {dealer.DealerReference} is already used by {sameReference.CompanyName}. Use a different number.");
            }
            if (string.IsNullOrWhiteSpace(login.Email) || string.IsNullOrWhiteSpace(login.PasswordHash) || string.IsNullOrWhiteSpace(login.Name))
            {
                return (false, "Enter the login's first name, email and password.");
            }
            if (await GetUserByEmail(login.Email) != null)
            {
                return (false, $"A user with email {login.Email} already exists. Use a different email for the dealer's login.");
            }

            login.PasswordHash = PasswordRules.Hash(login.PasswordHash);
            login.RoleId = UserRole.Dealer;
            login.Status = UserStatus.Active;
            login.IsActive = true;
            login.KnownAs = string.IsNullOrWhiteSpace(login.KnownAs) ? dealer.CompanyName : login.KnownAs;
            dealer.Email = string.IsNullOrWhiteSpace(dealer.Email) ? login.Email : dealer.Email;
            dealer.Address ??= "";
            dealer.Phone ??= "";

            var (created, createdLogin) = await _userRepository.CreateDealerWithLoginAsync(dealer, login);
            return (true, $"Created {created.CompanyName}: Dealer ID {created.DealerId}, dealer reference {created.DealerReference}, login user ID {createdLogin.UserId} ({createdLogin.Email}).");
        }

        private async Task<Dealer?> GetDealerByDealerRef(string dealerReference)
        {
            return await _userRepository.GetDealerByDealerRefAsync(dealerReference);
        }

        public async Task<IEnumerable<User>> GetAgentsByDealer(int dealerId)
        {
            var users = await _userRepository.GetAgentsByDealerIdAsync(dealerId);

            return users;
        }

        public async Task<IEnumerable<User>> GetAgents()
        {
            var users = await _userRepository.GetAgentsAsync();

            return users;
        }
        // similarly: GetById, Update, Delete
    }
}
