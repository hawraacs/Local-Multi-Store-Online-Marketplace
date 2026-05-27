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
    public class ComplaintRepository : Repository<Complaint>, IComplaintRepository
    {
        private readonly ApplicationDbContext _context;

        public ComplaintRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Complaint>> GetByCustomerAsync(int customerId)
        {
            return await _context.Complaints
                .Where(c => c.CustomerID == customerId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Complaint>> GetByStatusAsync(string status)
        {
            return await _context.Complaints
                .Where(c => c.Status == status)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Complaint>> GetByOrderAsync(int orderId)
        {
            return await _context.Complaints
                .Where(c => c.OrderID == orderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Complaint>> GetByProductAsync(int productId)
        {
            return await _context.Complaints
                .Where(c => c.ProductID == productId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Complaint>> GetByStoreAsync(int storeId)
        {
            return await _context.Complaints
                .Where(c => c.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Complaint>> GetLatestAsync(int count)
        {
            return await _context.Complaints
                .OrderByDescending(c => c.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}
