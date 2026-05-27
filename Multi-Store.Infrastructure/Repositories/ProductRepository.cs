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
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Product?> GetBySlugAsync(string slug)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.ProductSlug == slug);
        }

        public async Task<IReadOnlyList<Product>> GetByStoreAsync(int storeId)
        {
            return await _context.Products
                .Where(p => p.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetByCategoryAsync(int categoryId)
        {
            return await _context.Products
                .Where(p => p.CategoryID == categoryId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetActiveProductsAsync()
        {
            return await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync()
        {
            return await _context.Products
                .Where(p => p.Quantity <= p.LowStockThreshold)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetTopRatedProductsAsync(int count)
        {
            return await _context.Products
                .OrderByDescending(p => p.Rating)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> SearchProductsAsync(string keyword)
        {
            return await _context.Products
                .Where(p =>
                    p.ProductName.Contains(keyword) ||
                    p.Description.Contains(keyword))
                .ToListAsync();
        }

        public async Task<Product?> GetProductDetailsAsync(int productId)
        {
            return await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Category)
                .Include(p => p.Images)   // ✅ FIXED HERE
                .Include(p => p.CartItems)
                .Include(p => p.OrderItems)
                .Include(p => p.Wishlists)
                .FirstOrDefaultAsync(p => p.ProductID == productId);
        }
    }
}
