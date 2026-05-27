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
    public class CustomerAddressRepository : Repository<CustomerAddress>, ICustomerAddressRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerAddressRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerIdAsync(int customerId)
        {
            return await _context.CustomerAddresses
                .Where(a => a.CustomerID == customerId)
                .ToListAsync();
        }

        public async Task<CustomerAddress?> GetDefaultAddressAsync(int customerId)
        {
            return await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.CustomerID == customerId && a.IsDefault);
        }

        public async Task<IReadOnlyList<CustomerAddress>> GetActiveAddressesAsync(int customerId)
        {
            return await _context.CustomerAddresses
                .Where(a => a.CustomerID == customerId && a.IsActive)
                .ToListAsync();
        }

        public async Task SetAllAsNonDefaultAsync(int customerId)
        {
            var addresses = await _context.CustomerAddresses
                .Where(a => a.CustomerID == customerId && a.IsDefault)
                .ToListAsync();

            foreach (var address in addresses)
            {
                address.IsDefault = false;
            }

            await _context.SaveChangesAsync();
        }
    }
}

