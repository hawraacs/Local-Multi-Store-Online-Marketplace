using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class RecentlyViewedProductRepository
        : Repository<RecentlyViewedProduct>, IRecentlyViewedProductRepository
    {
        private readonly ApplicationDbContext _context;

        public RecentlyViewedProductRepository(ApplicationDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<RecentlyViewedProduct>> GetByCustomerAsync(int customerId)
        {
            return await _context.RecentlyViewedProducts
                .Where(x => x.CustomerID == customerId)
                .OrderByDescending(x => x.ViewedAt)
                .Take(10)
                .ToListAsync();
        }

        public async Task<RecentlyViewedProduct?> GetByCustomerAndProductAsync(
            int customerId,
            int productId)
        {
            return await _context.RecentlyViewedProducts
                .FirstOrDefaultAsync(x =>
                    x.CustomerID == customerId &&
                    x.ProductID == productId);
        }

        public async Task<bool> ExistsAsync(int customerId, int productId)
        {
            return await _context.RecentlyViewedProducts
                .AnyAsync(x =>
                    x.CustomerID == customerId &&
                    x.ProductID == productId);
        }
    }
}