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
    public class DeliveryPersonRepository : Repository<DeliveryPerson>, IDeliveryPersonRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryPersonRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<DeliveryPerson?> GetByUserIdAsync(int userId)
        {
            return await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.UserID == userId);
        }

        public async Task<IReadOnlyList<DeliveryPerson>> GetAvailableAsync()
        {
            return await _context.DeliveryPersons
                .Where(d => d.IsActive)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryPerson>> GetActiveAsync()
        {
            return await _context.DeliveryPersons
                .Where(d => d.IsActive)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryPerson>> GetTopRatedAsync(int count)
        {
            return await _context.DeliveryPersons
                .OrderByDescending(d => d.Rating)
                .Take(count)
                .ToListAsync();
        }

        public async Task<DeliveryPerson?> GetWithAssignmentsAsync(int deliveryPersonId)
        {
            return await _context.DeliveryPersons
                .Include(d => d.Assignments)
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == deliveryPersonId);
        }
     
       
    }
}
