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
    public class ReviewRepository : Repository<Review>, IReviewRepository
    {
        private readonly ApplicationDbContext _context;

        public ReviewRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Review>> GetByProductAsync(int productId)
        {
            return await _context.Reviews
                .Include(r => r.Customer)
                    .ThenInclude(c => c.User)
                .Where(r => r.ProductID == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
        public async Task<IReadOnlyList<Review>> GetByStoreAsync(int storeId)
        {
            return await _context.Reviews
                .Include(r => r.Customer)
                    .ThenInclude(c => c.User)
                .Where(r => r.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Review>> GetByCustomerAsync(int customerId)
        {
            return await _context.Reviews
                .Where(r => r.CustomerID == customerId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Review>> GetByStatusAsync(string status)
        {
            return await _context.Reviews
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsForOrderItemAsync(int orderItemId)
        {
            return await _context.Reviews
                .AnyAsync(r => r.OrderItemID == orderItemId);
        }
    }
}
