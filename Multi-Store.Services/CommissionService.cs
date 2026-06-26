using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    using global::Multi_Store.Infrastructure.Data;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Infrastructure.Data;

    namespace Multi_Store.Services
    {
        public class CommissionService
        {
            private readonly ApplicationDbContext _context;

            public CommissionService(ApplicationDbContext context)
            {
                _context = context;
            }

            public async Task CalculateMonthlyCommissionAsync()
            {
                var stores = await _context.Stores
                    .Where(s => s.Status == "Approved")
                    .ToListAsync();

                foreach (var store in stores)
                {
                    decimal monthlyRevenue = await _context.OrderItems
                        .Include(oi => oi.Order)
                        .Where(oi =>
                            oi.StoreID == store.StoreID &&
                            oi.Order.Status == "Delivered" &&
                            oi.Order.OrderDate.Month == DateTime.UtcNow.Month &&
                            oi.Order.OrderDate.Year == DateTime.UtcNow.Year)
                        .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0;

                    decimal commission = monthlyRevenue * 0.05m;

                    store.OutstandingBalance += commission;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
