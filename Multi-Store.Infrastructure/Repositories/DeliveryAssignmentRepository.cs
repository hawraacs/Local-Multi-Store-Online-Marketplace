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
    public class DeliveryAssignmentRepository : Repository<DeliveryAssignment>, IDeliveryAssignmentRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryAssignmentRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetByOrderAsync(int orderId)
        {
            return await _context.DeliveryAssignments
                .Where(a => a.OrderID == orderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetByDeliveryPersonAsync(int deliveryPersonId)
        {
            return await _context.DeliveryAssignments
                .Where(a => a.DeliveryPersonID == deliveryPersonId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetByStatusAsync(string status)
        {
            return await _context.DeliveryAssignments
                .Where(a => a.Status == status)
                .ToListAsync();
        }

        public async Task<DeliveryAssignment?> GetActiveAssignmentByOrderAsync(int orderId)
        {
            return await _context.DeliveryAssignments
                .FirstOrDefaultAsync(a => a.OrderID == orderId && a.Status == "Active");
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetTodayAssignmentsAsync(int deliveryPersonId)
        {
            var today = DateTime.UtcNow.Date;

            return await _context.DeliveryAssignments
                .Where(a =>
                    a.DeliveryPersonID == deliveryPersonId &&
                    a.AssignedAt.Date == today)
                .ToListAsync();
        }
    }
}
