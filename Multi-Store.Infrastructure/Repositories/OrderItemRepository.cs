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
    public class OrderItemRepository : Repository<OrderItem>, IOrderItemRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderItemRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<OrderItem>> GetByOrderAsync(int orderId)
        {
            return await _context.OrderItems
                .Include(oi => oi.Product)
                .Include(oi => oi.Store)
                .Where(oi => oi.OrderID == orderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<OrderItem>> GetByProductAsync(int productId)
        {
            return await _context.OrderItems
                .Where(oi => oi.ProductID == productId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<OrderItem>> GetByStoreAsync(int storeId)
        {
            return await _context.OrderItems
                .Where(oi => oi.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<OrderItem>> GetPendingReviewItemsAsync(int customerId)
        {
            return await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi =>
                    oi.Order.CustomerID == customerId &&
                    oi.ReviewSubmitted == false)
                .ToListAsync();
        }
    }
}
