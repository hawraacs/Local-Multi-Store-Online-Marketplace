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
    public class RefundRequestRepository : Repository<RefundRequest>, IRefundRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public RefundRequestRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<RefundRequest>> GetByCustomerAsync(int customerId)
        {
            return await _context.RefundRequests
                .Where(r => r.CustomerID == customerId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<RefundRequest>> GetByStatusAsync(string status)
        {
            return await _context.RefundRequests
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<RefundRequest?> GetByOrderAsync(int orderId)
        {
            return await _context.RefundRequests
                .Include(r => r.Order)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.OrderID == orderId);
        }

        public async Task<IReadOnlyList<RefundRequest>> GetPendingRequestsAsync()
        {
            return await _context.RefundRequests
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
