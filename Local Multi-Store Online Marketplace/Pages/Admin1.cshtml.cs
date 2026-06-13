using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class Admin1Model : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Admin1Model(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // DASHBOARD STATS
        // =========================

        public decimal TotalCommission { get; set; }
        public decimal TotalSubscriptionRevenue { get; set; }
        public decimal TotalPlatformRevenue { get; set; }

        public int TotalOrders { get; set; }
        public int ActiveStores { get; set; }
        public int TotalUsers { get; set; }

        public List<RecentOrderDto> RecentOrders { get; set; } = new();

        public async Task OnGetAsync()
        {
            // =========================
            // COMPLETED ORDERS
            // =========================
            var completedOrders = await _context.Orders
                .Where(o => o.Status == "Delivered")
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            // =========================
            // COMMISSION CALCULATION
            // =========================
            TotalCommission = completedOrders.Sum(order =>
                order.OrderItems.Sum(item =>
                    item.TotalPrice * (item.Store.CommissionRate / 100m)
                )
            );

            // =========================
            // SUBSCRIPTION REVENUE
            // =========================
            TotalSubscriptionRevenue = await _context.Stores
                .Where(s => s.LastPaymentAmount.HasValue)
                .SumAsync(s => s.LastPaymentAmount ?? 0);

            // =========================
            // TOTAL PLATFORM REVENUE
            // =========================
            TotalPlatformRevenue = TotalCommission + TotalSubscriptionRevenue;

            // =========================
            // BASIC COUNTS
            // =========================
            TotalOrders = await _context.Orders.CountAsync();

            ActiveStores = await _context.Stores.CountAsync(s =>
                s.Status == "Active" &&
                s.SubscriptionStatus == "Active");

            TotalUsers = await _context.Users.CountAsync();

            // =========================
            // RECENT ORDERS (SAFE VERSION)
            // =========================
            RecentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new RecentOrderDto
                {
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.Customer != null
                        ? o.Customer.User.FullName
                        : "Unknown",

                    StoreName = o.OrderItems
                        .Select(oi => oi.Store.StoreName)
                        .FirstOrDefault() ?? "N/A",

                    Amount = o.TotalAmount,
                    Status = o.Status
                })
                .ToListAsync();
        }
    }

    // =========================
    // DTO
    // =========================
    public class RecentOrderDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}