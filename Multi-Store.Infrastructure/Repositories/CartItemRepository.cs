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
    public class CartItemRepository : Repository<CartItem>, ICartItemRepository
    {
        private readonly ApplicationDbContext _context;

        public CartItemRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<CartItem>> GetItemsByCartAsync(int cartId)
        {
            return await _context.CartItems
                .Include(ci => ci.Product) // optional
                .Where(ci => ci.CartID == cartId)
                .ToListAsync();
        }

        public async Task<CartItem?> GetCartItemAsync(int cartId, int productId)
        {
            return await _context.CartItems
                .Include(ci => ci.Product) // optional
                .FirstOrDefaultAsync(ci =>
                    ci.CartID == cartId &&
                    ci.ProductID == productId);
        }
    }
}
