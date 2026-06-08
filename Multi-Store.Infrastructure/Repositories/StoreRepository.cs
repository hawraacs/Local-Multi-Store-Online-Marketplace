using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Infrastructure.Repositories
{
    public class StoreRepository : Repository<Store>, IStoreRepository
    {
        private readonly ApplicationDbContext _context;

        public StoreRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Store?> GetByCodeAsync(string storeCode)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(s => s.StoreCode == storeCode);
        }

        public async Task<IReadOnlyList<Store>> GetByStatusAsync(string status)
        {
            return await _context.Stores
                .Where(s => s.Status == status)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Store>> GetApprovedStoresAsync()
        {
            return await _context.Stores
                .Where(s => s.Status == "Approved")
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Store>> GetTopRatedStoresAsync(int count)
        {
            return await _context.Stores
                .OrderByDescending(s => s.Rating)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Store?> GetStoreDetailsAsync(int storeId)
        {
            return await _context.Stores
                .Include(s => s.Owner)
                .Include(s => s.Products)
                .Include(s => s.DeliveryAreas)
                .Include(s => s.Coupons)
                .Include(s => s.Reviews)
                .Include(s => s.Complaints)
                .FirstOrDefaultAsync(s => s.StoreID == storeId);
        }

        public async Task<IReadOnlyList<Store>> SearchStoresAsync(string keyword)
        {
            return await _context.Stores
                .Where(s =>
                    s.StoreName.Contains(keyword) ||
                    s.Description.Contains(keyword) ||
                    s.City.Contains(keyword) ||
                    s.Area.Contains(keyword))
                .ToListAsync();
        }
        public async Task<Store?> GetByOwnerIdAsync(int ownerUserId)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(s => s.OwnerUserID == ownerUserId);
        }
        public async Task<List<Product>> GetFeedProductsAsync(int customerId)
        {
            return await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .Where(p => _context.StoreFollows
                    .Any(f => f.CustomerID == customerId && f.StoreID == p.StoreID))
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();
        }


        public async Task<int> GetFollowersCountAsync(int storeId)
        {
            return await _context.StoreFollows
                .CountAsync(f => f.StoreID == storeId);
        }

        public async Task<List<Product>> GetStoreProductsAsync(int storeId)
        {
            return await _context.Products
                .Where(p => p.StoreID == storeId && p.IsActive)
                .Include(p => p.Images)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        public async Task FollowStoreAsync(int customerId, int storeId)
        {
            var exists = await _context.StoreFollows
                .AnyAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

            if (exists) return;

            _context.StoreFollows.Add(new StoreFollow
            {
                CustomerID = customerId,
                StoreID = storeId,
                FollowedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public async Task UnfollowStoreAsync(int customerId, int storeId)
        {
            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

            if (follow == null) return;

            _context.StoreFollows.Remove(follow);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsFollowingAsync(int customerId, int storeId)
        {
            return await _context.StoreFollows
                .AnyAsync(x => x.CustomerID == customerId && x.StoreID == storeId);
        }
        public async Task<List<Review>> GetStoreReviewsAsync(int storeId)
        {
            return await _context.Reviews
    .Include(r => r.Customer)
        .ThenInclude(c => c.User)
    .Where(r => r.StoreID == storeId)
    .OrderByDescending(r => r.CreatedAt)
    .ToListAsync();
        }
    }
}
