using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAnalyticsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdminAnalyticsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── KPI Cards ──
        public decimal RevenueInRange { get; set; }
        public decimal RevenueGrowth { get; set; }
        public int OrdersInRange { get; set; }
        public decimal AvgOrderValue { get; set; }
        public int TotalCustomers { get; set; }
        public int ActiveStores { get; set; }
        public int ActiveSubscriptions { get; set; }

        // ── Revenue Breakdown ──
        public decimal CommissionRevenue { get; set; }
        public decimal SubscriptionRevenue { get; set; }

        // ── Monthly Growth Comparison ──
        public decimal RevenuePrevious { get; set; }
        public int OrdersPrevious { get; set; }
        public int NewStoresCurrent { get; set; }
        public int NewStoresPrevious { get; set; }
        public int NewCustomersCurrent { get; set; }
        public int NewCustomersPrevious { get; set; }

        // ── Charts ──
        public List<string> MonthlyRevenueLabels { get; set; } = new();
        public List<decimal> MonthlyRevenueData { get; set; } = new();

        public List<string> MonthlyOrderLabels { get; set; } = new();
        public List<int> MonthlyOrderData { get; set; } = new();

        public List<string> MonthlyStoreLabels { get; set; } = new();
        public List<int> MonthlyStoreData { get; set; } = new();

        public List<string> MonthlyCustomerLabels { get; set; } = new();
        public List<int> MonthlyCustomerData { get; set; } = new();

        public Dictionary<string, int> OrderStatusDistribution { get; set; } = new();

        // ── Top Stores ──
        public List<TopStoreDto> TopStores { get; set; } = new();

        public async Task OnGetAsync()
        {
            var now = DateTime.Today;
            var rangeStart = now.AddDays(-30);
            var prevStart = now.AddDays(-60);

            // ── Active Subscriptions ──
            ActiveSubscriptions = await _context.Stores
                .CountAsync(s => s.SubscriptionStatus == "Active");

            // ── Current Orders (last 30 days) ──
            var currentOrders = await _context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= rangeStart)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            // ── Previous Orders (30–60 days ago) ──
            var prevOrders = await _context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= prevStart && o.OrderDate < rangeStart)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            // ── Revenue & Orders ──
            RevenueInRange = currentOrders
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m);

            RevenuePrevious = prevOrders
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m);

            RevenueGrowth = RevenuePrevious > 0
                ? ((RevenueInRange - RevenuePrevious) / RevenuePrevious) * 100
                : 0;

            OrdersInRange = currentOrders.Count;
            OrdersPrevious = prevOrders.Count;

            AvgOrderValue = OrdersInRange > 0
                ? currentOrders.Sum(o => o.TotalAmount) / OrdersInRange
                : 0;

            // ── Commission vs Subscription Revenue (last 30 days) ──
            CommissionRevenue = RevenueInRange; // same as total commission from current orders

            SubscriptionRevenue = await _context.SubscriptionPayments
                .Where(p => p.PaymentDate >= rangeStart)
                .SumAsync(p => p.Amount);

            // ── Customers ──
            TotalCustomers = await _context.Customers.CountAsync();

            // ── Active Stores (with at least one order in range) ──
            ActiveStores = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Delivered" && oi.Order.OrderDate >= rangeStart)
                .Select(oi => oi.StoreID)
                .Distinct()
                .CountAsync();

            // ── New Stores & New Customers (current vs previous period) ──
            NewStoresCurrent = await _context.Stores
                .CountAsync(s => s.CreatedAt >= rangeStart);

            NewStoresPrevious = await _context.Stores
                .CountAsync(s => s.CreatedAt >= prevStart && s.CreatedAt < rangeStart);

            NewCustomersCurrent = await _context.Customers
                .CountAsync(c => c.CreatedAt >= rangeStart);

            NewCustomersPrevious = await _context.Customers
                .CountAsync(c => c.CreatedAt >= prevStart && c.CreatedAt < rangeStart);

            // ── Top Stores ──
            TopStores = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Delivered" && oi.Order.OrderDate >= rangeStart)
                .Include(oi => oi.Store)
                .GroupBy(oi => new { oi.Store.StoreID, oi.Store.StoreName, oi.Store.Rating })
                .Select(g => new TopStoreDto
                {
                    Name = g.Key.StoreName,
                    Revenue = g.Sum(x => x.TotalPrice * x.Store.CommissionRate / 100m),
                    OrderCount = g.Select(x => x.OrderID).Distinct().Count(),
                    AvgOrderValue = g.Select(x => x.OrderID).Distinct().Count() > 0
                        ? g.Sum(x => x.TotalPrice) / g.Select(x => x.OrderID).Distinct().Count()
                        : 0,
                    Rating = (double)g.Key.Rating
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // ── Monthly Revenue (last 12 months) ──
            var completedOrders = await _context.Orders
                .Where(o => o.Status == "Delivered")
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);

                MonthlyRevenueLabels.Add(monthStart.ToString("MMM yyyy"));

                var monthlyCommission = completedOrders
                    .Where(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd)
                    .Sum(o => o.OrderItems.Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m));

                var monthlySubscriptions = await _context.SubscriptionPayments
                    .Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthEnd)
                    .SumAsync(p => p.Amount);

                MonthlyRevenueData.Add(monthlyCommission + monthlySubscriptions);
            }

            // ── Monthly Orders ──
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                MonthlyOrderLabels.Add(monthStart.ToString("MMM"));
                MonthlyOrderData.Add(await _context.Orders.CountAsync(o => o.OrderDate >= monthStart && o.OrderDate < monthEnd));
            }

            // ── Monthly New Stores ──
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                MonthlyStoreLabels.Add(monthStart.ToString("MMM"));
                MonthlyStoreData.Add(await _context.Stores.CountAsync(s => s.CreatedAt >= monthStart && s.CreatedAt < monthEnd));
            }

            // ── Monthly New Customers ──
            for (int i = 11; i >= 0; i--)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);
                MonthlyCustomerLabels.Add(monthStart.ToString("MMM"));
                MonthlyCustomerData.Add(await _context.Customers.CountAsync(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd));
            }

            // ── Order Status Distribution ──
            var statusCounts = await _context.Orders
                .Where(o => o.Status != null) // avoid null keys
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            OrderStatusDistribution = statusCounts.ToDictionary(x => x.Status, x => x.Count);
        }

        // ── API for dynamic sales chart ──
        public async Task<JsonResult> OnGetSalesAsync(int days = 30)
        {
            var since = DateTime.Today.AddDays(-days);
            var data = await _context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= since)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount) })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return new JsonResult(new
            {
                labels = data.Select(d => d.Date.ToString("MMM dd")),
                values = data.Select(d => d.Total)
            });
        }
    }

    public class TopStoreDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal AvgOrderValue { get; set; }
        public double Rating { get; set; }
    }
}