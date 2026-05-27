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
    public class CustomerRepository : Repository<Customer>, ICustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Customer?> GetByUserIdAsync(int userId)
        {
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == userId);
        }

        public async Task<Customer?> GetWithAddressesAsync(int customerId)
        {
            return await _context.Customers
                .Include(c => c.DefaultAddress)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);
        }

        public async Task<Customer?> GetWithOrdersAsync(int customerId)
        {
            return await _context.Customers
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);
        }

        public async Task<IReadOnlyList<Customer>> GetTopCustomersAsync(int count)
        {
            return await _context.Customers
                .OrderByDescending(c => c.Orders.Count)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Customer>> GetBlockedCODCustomersAsync()
        {
            return await _context.Customers
                .Where(c => c.CODBlocked)
                .ToListAsync();
        }
    }
}
