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
    public class WishlistRepository : Repository<Wishlist>, IWishlistRepository
    {
        private readonly ApplicationDbContext _context;

        public WishlistRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Wishlist>> GetByCustomerAsync(int customerId)
        {
            return await _context.Wishlists
                .Where(w => w.CustomerID == customerId)
                .Include(w => w.Product)
                    .ThenInclude(p => p.Images)
                .Include(w => w.Product)
                    .ThenInclude(p => p.Store)
                .OrderByDescending(w => w.AddedAt)
                .ToListAsync();
        }

        public async Task<Wishlist?> GetByCustomerAndProductAsync(int customerId, int productId)
        {
            return await _context.Wishlists
                .FirstOrDefaultAsync(w =>
                    w.CustomerID == customerId &&
                    w.ProductID == productId);
        }

        public async Task<bool> ExistsAsync(int customerId, int productId)
        {
            return await _context.Wishlists
                .AnyAsync(w =>
                    w.CustomerID == customerId &&
                    w.ProductID == productId);
        }
    }
}
