using com.sun.org.apache.xerces.@internal.xs.datatypes;
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
        public int NewStoresThisMonth { get; set; }
        public decimal UserGrowthPercentage { get; set; }
        public List<RecentStoreDto> RecentStores { get; set; } = new();
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
        public List<string> MonthlyRevenueLabels { get; set; } = new();
        public List<decimal> MonthlyRevenueData { get; set; } = new();

        public List<string> MonthlyOrderLabels { get; set; } = new();
        public List<int> MonthlyOrderData { get; set; } = new();

        public decimal RevenueGrowthPercentage { get; set; }

        public List<TopStoreDto> TopStores { get; set; } = new();
        public class TopStoreDto
        {
            public string StoreName { get; set; } = "";
            public decimal Revenue { get; set; }
        }
        public async Task OnGetAsync()
        {


            // =========================
            // COMPLETED ORDERS
            // =========================
            var thisMonthStart = new DateTime(DateTime.UtcNow.Date.Year, DateTime.UtcNow.Date.Month, 1);

            NewStoresThisMonth = await _context.Stores
                .CountAsync(s => s.CreatedAt >= thisMonthStart);

            TotalUsers = await _context.Users.CountAsync();

            var usersLastMonth = await _context.Users
                .CountAsync(u => u.CreatedAt < thisMonthStart);

            var usersThisMonth = TotalUsers;
            UserGrowthPercentage = usersLastMonth > 0
                ? ((decimal)(usersThisMonth - usersLastMonth) / usersLastMonth) * 100 : 0;

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
                    item.TotalPrice * 0.05m
                )
            );

            // =========================
            // SUBSCRIPTION REVENUE
            // =========================
            TotalSubscriptionRevenue = await _context.SubscriptionPayments
    .SumAsync(p => p.Amount);

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

            // ====================================
            // MONTHLY REVENUE - LAST 12 MONTHS
            // ====================================

            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(
                    DateTime.UtcNow.Date.AddMonths(-i).Year,
                    DateTime.UtcNow.Date.AddMonths(-i).Month,
                    1);

                var monthEnd = monthStart.AddMonths(1);

                MonthlyRevenueLabels.Add(monthStart.ToString("MMM yyyy"));

                var monthlyCommission = completedOrders
                    .Where(o =>
                        o.OrderDate >= monthStart &&
                        o.OrderDate < monthEnd)
                    .Sum(o =>
                        o.OrderItems.Sum(oi =>
                            oi.TotalPrice * 0.05m));

                var monthlySubscriptions = await _context.SubscriptionPayments
    .Where(p =>
        p.PaymentDate >= monthStart &&
        p.PaymentDate < monthEnd)
    .SumAsync(p => p.Amount);

                MonthlyRevenueData.Add(
                    monthlyCommission + monthlySubscriptions);
            }

            // ====================================
            // ORDERS PER MONTH
            // ====================================

            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(
                    DateTime.UtcNow.Date.AddMonths(-i).Year,
                    DateTime.UtcNow.Date.AddMonths(-i).Month,
                    1);

                var monthEnd = monthStart.AddMonths(1);

                MonthlyOrderLabels.Add(monthStart.ToString("MMM"));

                var count = await _context.Orders
                    .CountAsync(o =>
                        o.OrderDate >= monthStart &&
                        o.OrderDate < monthEnd);

                MonthlyOrderData.Add(count);
            }
            // ====================================
            // REVENUE GROWTH
            // ====================================

            var currentMonth = DateTime.UtcNow.Date;
            var currentStart =
                new DateTime(currentMonth.Year, currentMonth.Month, 1);

            var previousStart =
                currentStart.AddMonths(-1);

            var currentRevenue = MonthlyRevenueData.LastOrDefault();

            var previousRevenue = MonthlyRevenueData.Count > 1
                ? MonthlyRevenueData[MonthlyRevenueData.Count - 2]
                : 0;

            if (previousRevenue > 0)
            {
                RevenueGrowthPercentage =
                    ((currentRevenue - previousRevenue)
                    / previousRevenue) * 100;
            }
            // ====================================
            // TOP 5 STORES
            // ====================================

            TopStores = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Delivered")
                .GroupBy(oi => oi.Store.StoreName)
                .Select(g => new TopStoreDto
                {
                    StoreName = g.Key,
                    Revenue = g.Sum(x => x.TotalPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();
            RecentStores = await _context.Stores
    .OrderByDescending(s => s.CreatedAt)
    .Take(5)
    .Select(s => new RecentStoreDto
    {
        StoreName = s.StoreName,
        CreatedAt = s.CreatedAt,
        Status = s.Status
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
    public class RecentStoreDto
    {
        public string StoreName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "";
    }
}