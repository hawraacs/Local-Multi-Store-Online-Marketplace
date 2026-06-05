using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class RecentlyViewedManager
    {
        private readonly ApplicationDbContext _context;
        private readonly IRecentlyViewedProductRepository _recentlyViewedRepository;

        public RecentlyViewedManager(
            ApplicationDbContext context,
            IRecentlyViewedProductRepository recentlyViewedRepository)
        {
            _context = context;
            _recentlyViewedRepository = recentlyViewedRepository;
        }

        // =========================
        // ADD / UPDATE RECENTLY VIEWED
        // =========================
        public async Task AddViewAsync(int customerId, int productId)
        {
            var existing = await _recentlyViewedRepository
                .GetByCustomerAndProductAsync(customerId, productId);

            if (existing != null)
            {
                existing.ViewedAt = DateTime.UtcNow;
                await _recentlyViewedRepository.UpdateAsync(existing);
                return;
            }

            var recentlyViewed = new RecentlyViewedProduct
            {
                CustomerID = customerId,
                ProductID = productId,
                ViewedAt = DateTime.UtcNow
            };

            await _recentlyViewedRepository.AddAsync(recentlyViewed);
        }

        // =========================
        // GET CUSTOMER RECENTLY VIEWED
        // =========================
        public async Task<List<RecentlyViewedProductDTO>> GetCustomerRecentlyViewedAsync(int customerId)
        {
            var recentlyViewed = await _context.RecentlyViewedProducts
                .Where(rv => rv.CustomerID == customerId)
                .Join(
                    _context.Products
                        .Include(p => p.Images)
                        .Include(p => p.Store)
                        .Where(p => p.IsActive),
                    rv => rv.ProductID,
                    p => p.ProductID,
                    (rv, p) => new RecentlyViewedProductDTO
                    {
                        Id = rv.Id,
                        CustomerID = rv.CustomerID,
                        ProductID = p.ProductID,
                        ViewedAt = rv.ViewedAt,

                        ProductName = p.ProductName,
                        Price = p.Price,
                        StoreName = p.Store != null
                            ? p.Store.StoreName
                            : "Unknown Store",

                        IsOutOfStock = p.Quantity <= 0,

                        ImageUrl = p.Images
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.DisplayOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault() ?? "/images/no-image.png"
                    })
                .OrderByDescending(x => x.ViewedAt)
                .Take(10)
                .ToListAsync();

            return recentlyViewed;
        }
    }
}