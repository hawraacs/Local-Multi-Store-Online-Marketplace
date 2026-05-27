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
    }
}
