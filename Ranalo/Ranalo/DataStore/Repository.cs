using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class Repository : IRepository
    {
        private readonly AppDbContext _context;
        //private readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
           // _dbSet = context.Set<T>();
        }

        //public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

        //public async Task<T> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

        //public async Task AddAsync(T entity)
        //{
        //    await _dbSet.AddAsync(entity);
        //}

        //public void Update(T entity)
        //{
        //    _dbSet.Update(entity);
        //}

        //public void Delete(T entity)
        //{
        //    _dbSet.Remove(entity);
        //}

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<User> CreateUserAsync(User newUser)
        {
            try
            {
                // Add the user to the DbSet
                await _context.Users.AddAsync(newUser);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return newUser; // now it has the generated UserId
            }
            catch (Exception ex)
            {
                // log exception if needed
                throw;
            }
        }

        public async Task<User> UpdateUserAsync(User newUser)
        {
            try
            {
                // Update user to the DbSet
                _context.Users.Update(newUser);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return newUser; // now it has the generated UserId
            }
            catch (Exception ex)
            {
                // log exception if needed
                throw;
            }
        }


        public async Task<User> UpdateUserPasswordAsync(int userId, string newPasswordHash)
        {
            try
            {
                // Find the user by ID
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    throw new InvalidOperationException($"User with ID {userId} not found.");
                }

                // Update password
                user.PasswordHash = newPasswordHash; // Assume it's already hashed
                user.LastLogin = DateTime.UtcNow;    // Optional: track modification time

                // Save changes
                await _context.SaveChangesAsync();

                return user;
            }
            catch (Exception)
            {
                // Could log the exception before rethrowing
                throw;
            }
        }

        public async Task<User?> GetByEmailAndPasswordAsync(string email, string password)
        {
            try
            {
                return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password);
            }
            catch (Exception)
            {

                throw;
            }
            
        }
        public async Task<User?> GetByCustomerIdAsync(int userId)
        {
            try
            {
                return await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users.AsNoTracking().ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<IEnumerable<User>> GetUsersByDealerIdAsync(int dealerId)
        {
            try
            {
                return await _context.Users
                .Where(x => x.DealerId == dealerId)
                .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<Dealer?> GetDealerByUserIdAsync(int userId)
        {
            try
            {
                return await _context.Dealers
                .FirstOrDefaultAsync(u => u.UserId == userId);
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<User?> GetUserByPasswordAsync(string password)
        {
            try
            {
                return await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordHash == password);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task UpdateUserLastLogin(User user)
        {
            try
            {
                // Find the user by ID
                var currentUser = await _context.Users.FindAsync(user.UserId);
                if (currentUser == null)
                {
                    throw new InvalidOperationException($"User with ID {currentUser.UserId} not found.");
                }

                currentUser.LastLogin = DateTime.UtcNow;    // Optional: track modification time

                // Save changes
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Could log the exception before rethrowing
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetDebtCollectorsAsync()
        {
            try
            {
                var users = await _context.Users.ToListAsync();

                return users
                    .Where(x =>
                        x.RoleId == UserRole.Collector ||
                        (x.OtherSelectedRoles != null &&
                         x.OtherSelectedRoles.Contains("Collector"))
                    )
                    .ToList();
            }
            catch (Exception)
            {

                throw;
            }
        }

        //// DEALERS 
        ///
        public async Task<IEnumerable<Dealer>> GetAllDealersAsync()
        {
            try
            {
                return await _context.Dealers.AsNoTracking().ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<Dealer?> GetDealerByDealerRefAsync(string dealerReference)
        {
            try
            {
                return await _context.Dealers
                .FirstOrDefaultAsync(x => x.DealerReference == dealerReference);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task CreateDealerAsync(Dealer dealerDetails)
        {
            try
            {
                // Add the user to the DbSet
                await _context.Dealers.AddAsync(dealerDetails);

                // Save changes to the database
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // log exception if needed
                throw;
            }
        }
    }
}
