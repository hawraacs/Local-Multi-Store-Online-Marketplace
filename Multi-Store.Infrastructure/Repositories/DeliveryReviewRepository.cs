using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class DeliveryReviewRepository : Repository<DeliveryReview>, IDeliveryReviewRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryReviewRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> ExistsForOrderAsync(int orderId)
        {
            return await _context.DeliveryReviews
                .AnyAsync(r => r.OrderID == orderId);
        }

        public async Task<IReadOnlyList<DeliveryReview>> GetByDeliveryPersonAsync(int deliveryPersonId)
        {
            return await _context.DeliveryReviews
                .Include(r => r.Customer)
                    .ThenInclude(c => c.User)
                .Where(r => r.DeliveryPersonID == deliveryPersonId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
