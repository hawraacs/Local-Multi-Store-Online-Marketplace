using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System;
using System.Collections.Generic;
using System.Linq;
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
                .Include(a => a.Order)
                .Include(a => a.DeliveryPerson)
                .Where(a => a.OrderID == orderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetByDeliveryPersonAsync(int deliveryPersonId)
        {
            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o.Address)
                .Where(a => a.DeliveryPersonID == deliveryPersonId)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetByStatusAsync(string status)
        {
            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                .Include(a => a.DeliveryPerson)
                .Where(a => a.Status == status)
                .ToListAsync();
        }

        public async Task<DeliveryAssignment?> GetActiveAssignmentByOrderAsync(int orderId)
        {
            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                .Include(a => a.DeliveryPerson)
                .FirstOrDefaultAsync(a =>
                    a.OrderID == orderId &&
                    a.Status != "Delivered" &&
                    a.Status != "Cancelled");
        }

        public async Task<IReadOnlyList<DeliveryAssignment>> GetTodayAssignmentsAsync(int deliveryPersonId)
        {
            var today = DateTime.UtcNow.Date;

            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o.Address)
                .Where(a =>
                    a.DeliveryPersonID == deliveryPersonId &&
                    a.AssignedAt.Date == today)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();
        }
    }
}