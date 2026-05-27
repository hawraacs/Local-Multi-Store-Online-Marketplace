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
    public class OrderStatusHistoryRepository : Repository<OrderStatusHistory>, IOrderStatusHistoryRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderStatusHistoryRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<OrderStatusHistory>> GetByOrderAsync(int orderId)
        {
            return await _context.OrderStatusHistories
                .Where(h => h.OrderID == orderId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();
        }

        public async Task<OrderStatusHistory?> GetLatestStatusAsync(int orderId)
        {
            return await _context.OrderStatusHistories
                .Where(h => h.OrderID == orderId)
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();
        }
    }
}
