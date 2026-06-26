using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Core.Entities;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAnalyticsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public int ActiveSubscriptions { get; set; }
        public AdminAnalyticsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── KPI Stats ──
        public decimal RevenueInRange { get; set; }
        public decimal RevenueGrowth { get; set; }
        public int OrdersInRange { get; set; }
        public decimal AvgOrderValue { get; set; }
        public int NewCustomers { get; set; }
        public int ActiveStores { get; set; }

        public List<TopStoreDto> TopStores { get; set; } = new();

        public async Task OnGetAsync()
        {
            var now = DateTime.Today;
            var rangeStart = now.AddDays(-30);
            var prevStart = now.AddDays(-60);

            ActiveSubscriptions = await _context.Stores
    .CountAsync(s => s.SubscriptionStatus == "Active");

            // ── Current Orders ──
            var currentOrders = await _context.Orders
                .Where(o => o.Status == "Delivered"
                         && o.OrderDate >= rangeStart)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            RevenueInRange = currentOrders
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m);

            // ── Previous Orders ──
            var prevOrders = await _context.Orders
                .Where(o => o.Status == "Delivered"
                         && o.OrderDate >= prevStart
                         && o.OrderDate < rangeStart)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            var prevRevenue = prevOrders
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m);

            RevenueGrowth = prevRevenue > 0
                ? ((RevenueInRange - prevRevenue) / prevRevenue) * 100
                : 0;

            // ── Orders ──
            OrdersInRange = currentOrders.Count;

            AvgOrderValue = OrdersInRange > 0
                ? currentOrders.Sum(o => o.TotalAmount) / OrdersInRange
                : 0;

            // ── FIX: New customers (no CreatedAt field exists) ──
            NewCustomers = await _context.Customers.CountAsync();

            // ── Active Stores ──
            ActiveStores = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Delivered"
                          && oi.Order.OrderDate >= rangeStart)
                .Select(oi => oi.StoreID)
                .Distinct()
                .CountAsync();

            // ── Top Stores ──
            TopStores = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Delivered"
                          && oi.Order.OrderDate >= rangeStart)
                .Include(oi => oi.Store)
                .GroupBy(oi => new { oi.Store.StoreID, oi.Store.StoreName, oi.Store.Rating })
                .Select(g => new TopStoreDto
                {
                    Name = g.Key.StoreName,
                    Revenue = g.Sum(x => x.TotalPrice * x.Store.CommissionRate / 100m),
                    OrderCount = g.Select(x => x.OrderID).Distinct().Count(),
                    AvgOrderValue = g.Sum(x => x.TotalPrice) /
                                    g.Select(x => x.OrderID).Distinct().Count(),
                    Rating = (double)g.Key.Rating
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();
        }

        // ── Chart API ──
        public async Task<JsonResult> OnGetSalesAsync(int days = 30)
        {
            var since = DateTime.Today.AddDays(-days);

            var data = await _context.Orders
                .Where(o => o.Status == "Delivered" && o.OrderDate >= since)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(o => o.TotalAmount)
                })
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