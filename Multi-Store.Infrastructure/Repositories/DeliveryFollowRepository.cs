using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System.Collections.Generic;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class DeliveryFollowRepository : Repository<DeliveryFollow>, IDeliveryFollowRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryFollowRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<DeliveryFollow?> GetByCustomerAndDeliveryPersonAsync(int customerId, int deliveryPersonId)
        {
            return await _context.DeliveryFollows
                .FirstOrDefaultAsync(f =>
                    f.CustomerID == customerId &&
                    f.DeliveryPersonID == deliveryPersonId);
        }

        public async Task<IReadOnlyList<DeliveryFollow>> GetByCustomerAsync(int customerId)
        {
            return await _context.DeliveryFollows
                .Where(f => f.CustomerID == customerId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryFollow>> GetByDeliveryPersonAsync(int deliveryPersonId)
        {
            return await _context.DeliveryFollows
                .Where(f => f.DeliveryPersonID == deliveryPersonId)
                .ToListAsync();
        }

        public async Task<int> GetFollowersCountAsync(int deliveryPersonId)
        {
            return await _context.DeliveryFollows
                .CountAsync(f => f.DeliveryPersonID == deliveryPersonId);
        }

        public async Task<bool> IsFollowingAsync(int customerId, int deliveryPersonId)
        {
            return await _context.DeliveryFollows
                .AnyAsync(f =>
                    f.CustomerID == customerId &&
                    f.DeliveryPersonID == deliveryPersonId);
        }
    }
}
