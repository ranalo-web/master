using System;
using Microsoft.EntityFrameworkCore;
using Ranalo.DataStore.DataModels;

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
    }
}
