using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Ranalo.DataStore.DataModels;
using Ranalo.Models;

namespace Ranalo.DataStore
{
    public class EnrolmentRepository : IEnrolmentRepository
    {
        private readonly AppDbContext _context;

        public EnrolmentRepository(AppDbContext context)
        {
            _context = context;
            // _dbSet = context.Set<T>();
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Enrolment> CreateEnrolmentAsync(Enrolment newEnrolment)
        {
            try
            {
                // Add the user to the DbSet
                await _context.Enrolments.AddAsync(newEnrolment);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return newEnrolment; // now it has the generated UserId
            }
            catch (Exception)
            {
                // log exception if needed
                throw;
            }
        }

        public async Task<Enrolment> UpdateEnrolmentAsync(Enrolment updateEnrolment)
        {
            try
            {
                // Update user to the DbSet
                _context.Enrolments.Update(updateEnrolment);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return updateEnrolment; // now it has the generated UserId
            }
            catch (Exception)
            {
                // log exception if needed
                throw;
            }
        }


        public async Task<Enrolment> UpdateEnrolmentPasswordAsync(int userId, string newPasswordHash)
        {
            try
            {
                // Find the user by ID
                var user = await _context.Enrolments.FindAsync(userId);
                if (user == null)
                {
                    throw new InvalidOperationException($"User with ID {userId} not found.");
                }

                //// Update password
                //user.PasswordHash = newPasswordHash; // Assume it's already hashed
                //user.LastLogin = DateTime.UtcNow;    // Optional: track modification time

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

        public async Task<Enrolment?> GetByImeiNumberAsync(string imei)
        {
            try
            {
                return await _context.Enrolments
                .FirstOrDefaultAsync(u => u.IMEI == imei);
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<Enrolment?> GetByAccountIdAsync(long accountId)
        {
            try
            {
                // An unsold stock phone has no account (0) -- never let a
                // lookup for account 0 find one and lock or release it.
                return await _context.Enrolments
                .FirstOrDefaultAsync(u => u.AccountId == accountId && u.Status != EnrolmentStatus.Stock);
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<IEnumerable<Enrolment>> GetAllEnrolmentsAsync()
        {
            try
            {
                return await _context.Enrolments.AsNoTracking().ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetAllEnrolmentsAsync(int pageNumber, int pageSize, string? searchTerm = null)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 10;

            // Unsold stock phones have their own page (StockPhonesController).
            var query = ApplyEnrolmentSearch(_context.Enrolments.AsNoTracking().Where(x => x.Status != EnrolmentStatus.Stock), searchTerm);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.Created) // always order before Skip
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<Enrolment> Items, int TotalCount)>
        GetDealerEnrolmentsAsync(int dealerId, int pageNumber, int pageSize, string? searchTerm = null)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = ApplyEnrolmentSearch(
                _context.Enrolments.AsNoTracking().Where(x => x.DealerId == dealerId && x.Status != EnrolmentStatus.Stock),
                searchTerm);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.Created) // always order before Skip
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        private static IQueryable<Enrolment> ApplyEnrolmentSearch(IQueryable<Enrolment> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return query;
            }

            return query.Where(x =>
                x.IMEI.Contains(searchTerm)
                || (x.FirstName != null && x.FirstName.Contains(searchTerm))
                || (x.LastName != null && x.LastName.Contains(searchTerm))
                || x.AccountId.ToString().Contains(searchTerm)
                || x.OrderId.ToString().Contains(searchTerm));
        }

        // Stock phones, newest first; unverifiedOnly = not yet confirmed locked.
        // Touched in the last 14 days (a stock phone's sale updates it), so old
        // enrolments approvers never approved aren't checked every run.
        public async Task<List<Enrolment>> GetAwaitingApprovalAsync()
        {
            var since = DateTime.UtcNow.AddDays(-14);
            return await _context.Enrolments.AsNoTracking()
                .Where(x => x.CallApprovedAt == null && !x.DepositShort && x.BrandOverriddenByUserId == null && x.OrderId > 0
                    && (x.Status == EnrolmentStatus.Pending || x.Status == EnrolmentStatus.PendingActivation || x.Status == EnrolmentStatus.Enrolled)
                    && x.Updated >= since)
                .ToListAsync();
        }

        public async Task<List<Enrolment>> GetStockAsync(bool unverifiedOnly, string? searchTerm = null)
        {
            var query = _context.Enrolments.AsNoTracking().Where(x => x.Status == EnrolmentStatus.Stock);
            if (unverifiedOnly)
            {
                query = query.Where(x => x.StockVerifiedAt == null);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => x.IMEI.Contains(searchTerm) || (x.DeviceBrand != null && x.DeviceBrand.Contains(searchTerm)));
            }

            return await query.OrderByDescending(x => x.StockLockedAt).ToListAsync();
        }

        public async Task<IEnumerable<Enrolment>> GetEnrolmentsByDealerIdAsync(int dealerId)
        {
            try
            {
                return await _context.Enrolments
                .Where(x => x.DealerId == dealerId)
                .ToListAsync();
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<Enrolment> GetByEnrolmentIdAsync(Guid enrolmentId)
        {
            try
            {
                return await _context.Enrolments
                .FirstOrDefaultAsync(u => u.Id == enrolmentId);
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<bool> DeleteEnrolmentAsync(Enrolment enrolment)
        {
            try
            {
                _context.Enrolments.Attach(enrolment);
                _context.Enrolments.Remove(enrolment);

                var result = await _context.SaveChangesAsync();

                return result > 0;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false; // Not found
            }
        }
    }
}
