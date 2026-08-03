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
    public class DeliveryReviewRepository : Repository<DeliveryReview>, IDeliveryReviewRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryReviewRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
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

        public async Task<IReadOnlyList<DeliveryReview>> GetByCustomerAsync(int customerId)
        {
            return await _context.DeliveryReviews
                .Where(r => r.CustomerID == customerId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryReview>> GetByStatusAsync(string status)
        {
            return await _context.DeliveryReviews
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsForAssignmentAsync(int assignmentId)
        {
            return await _context.DeliveryReviews
                .AnyAsync(r => r.AssignmentID == assignmentId);
        }
    }
}