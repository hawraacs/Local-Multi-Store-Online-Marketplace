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
    public class CartRepository : Repository<Cart>, ICartRepository
    {
        private readonly ApplicationDbContext _context;

        public CartRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Cart?> GetCartByCustomerAsync(int customerId)
        {
            return await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product) // optional
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);
        }

        public async Task<Cart?> GetCartBySessionTokenAsync(string sessionToken)
        {
            return await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product) // optional
                .FirstOrDefaultAsync(c => c.SessionToken == sessionToken);
        }

        public async Task<Cart?> GetCartWithItemsAsync(int cartId)
        {
            return await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product) // optional
                .FirstOrDefaultAsync(c => c.CartID == cartId);
        }
    }
}
