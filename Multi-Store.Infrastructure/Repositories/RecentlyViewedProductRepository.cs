using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class RecentlyViewedProductRepository
        : Repository<RecentlyViewedProduct>,
          IRecentlyViewedProductRepository
    {
        private readonly ApplicationDbContext _context;

        public RecentlyViewedProductRepository(
            ApplicationDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<RecentlyViewedProduct>> GetByCustomerAsync(int customerId)
        {
            return await _context.RecentlyViewedProducts
                .Where(x => x.CustomerID == customerId)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Images)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Store)
                .OrderByDescending(x => x.ViewedAt)
                .Take(10)
                .ToListAsync();
        }

        public async Task AddViewAsync(int customerId, int productId)
        {
            var existingItem = await _context.RecentlyViewedProducts
                .FirstOrDefaultAsync(x =>
                    x.CustomerID == customerId &&
                    x.ProductID == productId);

            if (existingItem != null)
            {
                existingItem.ViewedAt = DateTime.UtcNow;
            }
            else
            {
                var item = new RecentlyViewedProduct
                {
                    CustomerID = customerId,
                    ProductID = productId,
                    ViewedAt = DateTime.UtcNow
                };

                await _context.RecentlyViewedProducts.AddAsync(item);
            }

            await _context.SaveChangesAsync();
        }
    }
}