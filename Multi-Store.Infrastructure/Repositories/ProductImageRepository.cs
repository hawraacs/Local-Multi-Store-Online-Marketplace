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
    public class ProductImageRepository : Repository<ProductImage>, IProductImageRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductImageRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ProductImage>> GetByProductAsync(int productId)
        {
            return await _context.ProductImages
                .Where(i => i.ProductID == productId)
                .OrderBy(i => i.DisplayOrder)
                .ToListAsync();
        }

        public async Task<ProductImage?> GetPrimaryImageAsync(int productId)
        {
            return await _context.ProductImages
                .FirstOrDefaultAsync(i => i.ProductID == productId && i.IsPrimary);
        }

        public async Task<int> GetMaxDisplayOrderAsync(int productId)
        {
            return await _context.ProductImages
                .Where(i => i.ProductID == productId)
                .Select(i => (int?)i.DisplayOrder)
                .MaxAsync() ?? 0;
        }
    
}
}
