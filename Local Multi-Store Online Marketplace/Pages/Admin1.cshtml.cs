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

        // ── KPI Cards ──
        public decimal TotalPlatformRevenue { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalSubscriptionRevenue { get; set; }
        public decimal TotalBoostRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int ActiveStores { get; set; }
        public int TotalUsers { get; set; }
        public int NewStoresThisMonth { get; set; }
        public decimal UserGrowthPercentage { get; set; }

        // ── Operational Info ──
        public int PendingStoreApprovals { get; set; }
        public int StoresExpiringSoon { get; set; }
        public int StoresWaitingForRenewal { get; set; }

        // ── Quick Alerts ──
        public List<StoreAlertDto> SuspendedStores { get; set; } = new();
        public List<StoreAlertDto> ExpiredStores { get; set; } = new();
        public List<StoreAlertDto> PendingStoreRequests { get; set; } = new();

        // ── Recent Activity ──
        public List<RecentOrderDto> RecentOrders { get; set; } = new();
        public List<RecentStoreDto> RecentStores { get; set; } = new();

        public async Task OnGetAsync()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // ── KPI Calculations ──

            // 1. Total completed orders for commission
            var completedOrders = await _context.Orders
                .Where(o => o.Status == "Delivered")
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Store)
                .ToListAsync();

            TotalCommission = completedOrders
                .SelectMany(o => o.OrderItems)
                .Sum(oi => oi.TotalPrice * oi.Store.CommissionRate / 100m);

            TotalSubscriptionRevenue = await _context.SubscriptionPayments
                .SumAsync(p => p.Amount);

            // Total boost revenue — same booking rule as AdminRevenue/AdminBoosts:
            // counted once a boost has been paid for (Active or Expired).
            TotalBoostRevenue = await _context.ProductBoosts
                .Where(b => b.Status == "Active" || b.Status == "Expired")
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0m;

            TotalPlatformRevenue = TotalCommission + TotalSubscriptionRevenue + TotalBoostRevenue;

            TotalOrders = await _context.Orders.CountAsync();

            // FIXED — Active Stores: Status == "Approved" (this is the value your app actually
            // sets when a store is approved, confirmed by Customer1Model/CustomerFeedModel both
            // filtering on p.Store.Status == "Approved" everywhere products are shown to customers).
            // SubscriptionStatus is the field that genuinely uses "Active".
            ActiveStores = await _context.Stores
                .CountAsync(s => s.Status == "Approved" && s.SubscriptionStatus == "Active");

            TotalUsers = await _context.Users.CountAsync();

            NewStoresThisMonth = await _context.Stores
                .CountAsync(s => s.CreatedAt >= monthStart);

            var usersLastMonth = await _context.Users
                .CountAsync(u => u.CreatedAt < monthStart);

            UserGrowthPercentage = usersLastMonth > 0
                ? ((decimal)(TotalUsers - usersLastMonth) / usersLastMonth) * 100
                : 0;

            // ── Operational Info ──

            // Pending store approvals (Status == "Pending")
            PendingStoreApprovals = await _context.Stores
                .CountAsync(s => s.Status == "Pending");

            // Stores with subscription expiring within next 7 days (SubscriptionExpiryDate)
            var expiryThreshold = now.AddDays(7);
            StoresExpiringSoon = await _context.Stores
                .CountAsync(s => s.SubscriptionStatus == "Active"
                                 && s.SubscriptionExpiryDate.HasValue
                                 && s.SubscriptionExpiryDate <= expiryThreshold
                                 && s.SubscriptionExpiryDate >= now);

            // Stores waiting for renewal (SubscriptionStatus == "Expired" or "Suspended")
            StoresWaitingForRenewal = await _context.Stores
                .CountAsync(s => s.SubscriptionStatus == "Expired" || s.SubscriptionStatus == "Suspended");

            // ── Quick Alerts ──

            // Suspended stores: Status == "Suspended" (or you could use IsSuspended == true)
            SuspendedStores = await _context.Stores
                .Where(s => s.Status == "Suspended")
                .Select(s => new StoreAlertDto
                {
                    StoreName = s.StoreName,
                    Status = s.Status,
                    AlertDate = s.ApprovedAt ?? s.CreatedAt   // fallback to CreatedAt if not approved
                })
                .Take(5)
                .ToListAsync();

            // Expired subscriptions: SubscriptionStatus == "Expired"
            ExpiredStores = await _context.Stores
                .Where(s => s.SubscriptionStatus == "Expired")
                .Select(s => new StoreAlertDto
                {
                    StoreName = s.StoreName,
                    Status = "Expired",
                    AlertDate = s.SubscriptionExpiryDate ?? s.CreatedAt
                })
                .Take(5)
                .ToListAsync();

            // Pending store requests: Status == "Pending"
            PendingStoreRequests = await _context.Stores
                .Where(s => s.Status == "Pending")
                .Select(s => new StoreAlertDto
                {
                    StoreName = s.StoreName,
                    Status = "Pending",
                    AlertDate = s.CreatedAt
                })
                .Take(5)
                .ToListAsync();

            // ── Recent Activity ──

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

    // ── DTOs ──
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
        public string StoreName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class StoreAlertDto
    {
        public string StoreName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime AlertDate { get; set; }
    }
}