using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Infrastructure.Repositories
{
    public class DeliveryPaymentCollectionRepository
        : Repository<DeliveryPaymentCollection>, IDeliveryPaymentCollectionRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryPaymentCollectionRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<DeliveryPaymentCollection?> GetByOrderAsync(int orderId)
        {
            return await _context.DeliveryPaymentCollections
                .FirstOrDefaultAsync(c => c.OrderID == orderId);
        }

        public async Task<IReadOnlyList<DeliveryPaymentCollection>> GetByDeliveryPersonAsync(int deliveryPersonId)
        {
            return await _context.DeliveryPaymentCollections
                .Where(c => c.DeliveryPersonID == deliveryPersonId)
                .OrderByDescending(c => c.CollectionDate)
                .ToListAsync();
        }
    }
}