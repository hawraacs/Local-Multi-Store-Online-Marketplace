using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class AnalyticsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public AnalyticsModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string Range { get; set; } = "30";

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public DateTime ReportStartDate { get; set; }

        public DateTime ReportEndDate { get; set; }

        public decimal TotalRevenue { get; set; }

        public decimal PendingRevenue { get; set; }

        public int TotalOrders { get; set; }

        public int DeliveredOrdersCount { get; set; }

        public int TotalItemsSold { get; set; }

        public int UniqueCustomers { get; set; }

        public decimal AverageOrderValue { get; set; }

        public decimal EstimatedCommission { get; set; }

        public decimal EstimatedNetEarnings { get; set; }

        public int PendingOrders { get; set; }

        public int PreparingOrders { get; set; }

        public int OutForDeliveryOrders { get; set; }

        public int DeliveredOrders { get; set; }

        public int CancelledOrders { get; set; }

        // ── BOOST ANALYTICS (NEW) ──
        public decimal BoostSpendInRange { get; set; }
        public int BoostsStartedInRange { get; set; }
        public int CurrentActiveBoostsCount { get; set; }
        public List<BoostPerformanceViewModel> BoostedProductPerformance { get; set; } = new();

        public List<BestSellingProductViewModel> BestSellingProducts { get; set; } = new();

        public List<DailyRevenueViewModel> DailyRevenue { get; set; } = new();

        public List<RecentOrderViewModel> RecentOrders { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.OwnerUserID == user.Id);

            if (store == null)
            {
                TempData["Error"] = "No store is connected to your account.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            StoreName = store.StoreName;

            var dateRange = GetDateRange();

            ReportStartDate = dateRange.StartDate;
            ReportEndDate = dateRange.EndDate;

            var storeOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi =>
                    oi.StoreID == store.StoreID &&
                    oi.Order != null &&
                    oi.Order.OrderDate >= ReportStartDate &&
                    oi.Order.OrderDate <= ReportEndDate)
                .ToListAsync();

            var storeOrderIds = storeOrderItems
                .Select(oi => oi.OrderID)
                .Distinct()
                .ToList();

            var storeOrders = await _context.Orders
                .Where(o => storeOrderIds.Contains(o.OrderID))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var deliveredOrdersList = storeOrders
                .Where(o => IsDelivered(o.Status))
                .ToList();

            var deliveredOrderIds = deliveredOrdersList
                .Select(o => o.OrderID)
                .ToList();

            var activeOrderIds = storeOrders
                .Where(o => IsActiveOrder(o.Status))
                .Select(o => o.OrderID)
                .ToList();

            var deliveredOrderItems = storeOrderItems
                .Where(oi => deliveredOrderIds.Contains(oi.OrderID))
                .ToList();

            var activeOrderItems = storeOrderItems
                .Where(oi => activeOrderIds.Contains(oi.OrderID))
                .ToList();

            TotalRevenue = deliveredOrderItems.Sum(oi => oi.TotalPrice);

            PendingRevenue = activeOrderItems.Sum(oi => oi.TotalPrice);

            TotalOrders = storeOrders.Count;

            DeliveredOrdersCount = deliveredOrdersList.Count;

            TotalItemsSold = deliveredOrderItems.Sum(oi => oi.Quantity);

            UniqueCustomers = storeOrders
                .Select(o => o.CustomerID)
                .Distinct()
                .Count();

            AverageOrderValue = DeliveredOrdersCount > 0
                ? Math.Round(TotalRevenue / DeliveredOrdersCount, 2)
                : 0;

            EstimatedCommission = Math.Round(TotalRevenue * (store.CommissionRate / 100m), 2);

            EstimatedNetEarnings = TotalRevenue - EstimatedCommission;

            PendingOrders = storeOrders.Count(o => IsPending(o.Status));

            PreparingOrders = storeOrders.Count(o => IsPreparing(o.Status));

            OutForDeliveryOrders = storeOrders.Count(o => IsOutForDelivery(o.Status));

            DeliveredOrders = storeOrders.Count(o => IsDelivered(o.Status));

            CancelledOrders = storeOrders.Count(o => IsCancelled(o.Status));

            BestSellingProducts = deliveredOrderItems
                .GroupBy(oi => new
                {
                    oi.ProductID,
                    oi.ProductName
                })
                .Select(g => new BestSellingProductViewModel
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.TotalPrice)
                })
                .OrderByDescending(x => x.QuantitySold)
                .ThenByDescending(x => x.Revenue)
                .Take(6)
                .ToList();

            DailyRevenue = deliveredOrderItems
                .Where(oi => oi.Order != null)
                .GroupBy(oi => oi.Order.OrderDate.Date)
                .Select(g => new DailyRevenueViewModel
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalPrice),
                    Orders = g.Select(x => x.OrderID).Distinct().Count(),
                    ItemsSold = g.Sum(x => x.Quantity)
                })
                .OrderBy(x => x.Date)
                .ToList();

            RecentOrders = storeOrders
                .Take(8)
                .Select(order => new RecentOrderViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    Amount = storeOrderItems
                        .Where(oi => oi.OrderID == order.OrderID)
                        .Sum(oi => oi.TotalPrice)
                })
                .ToList();

            // ── BOOST ANALYTICS (NEW) ──
            await LoadBoostAnalyticsAsync(store.StoreID, deliveredOrderItems);

            return Page();
        }

        private async Task LoadBoostAnalyticsAsync(int storeId, List<OrderItem> deliveredOrderItemsInRange)
        {
            // Money actually spent starting a boost within the selected date range,
            // counted once paid for (Active or Expired) — same rule used across the
            // admin boost/revenue pages, scoped here to CreatedAt within the range.
            BoostSpendInRange = await _context.ProductBoosts
                .Where(b => b.StoreID == storeId
                    && (b.Status == "Active" || b.Status == "Expired")
                    && b.CreatedAt >= ReportStartDate
                    && b.CreatedAt <= ReportEndDate)
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0m;

            BoostsStartedInRange = await _context.ProductBoosts
                .CountAsync(b => b.StoreID == storeId
                    && b.CreatedAt >= ReportStartDate
                    && b.CreatedAt <= ReportEndDate);

            // Live count, independent of the date range — "how many are boosted right now"
            var now = DateTime.UtcNow;
            CurrentActiveBoostsCount = await _context.ProductBoosts
                .CountAsync(b => b.StoreID == storeId && b.Status == "Active" && b.EndDate > now);

            // Performance of products that have ever been boosted, using the same
            // delivered-order-items-in-range figures already computed above.
            var boostedProductIds = await _context.ProductBoosts
                .Where(b => b.StoreID == storeId)
                .Select(b => b.ProductID)
                .Distinct()
                .ToListAsync();

            if (boostedProductIds.Count == 0)
            {
                BoostedProductPerformance = new List<BoostPerformanceViewModel>();
                return;
            }

            var products = await _context.Products
                .Where(p => boostedProductIds.Contains(p.ProductID))
                .Select(p => new { p.ProductID, p.ProductName })
                .ToListAsync();

            var activeBoosts = await _context.ProductBoosts
                .Where(b => boostedProductIds.Contains(b.ProductID) && b.StoreID == storeId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            BoostedProductPerformance = products
                .Select(p =>
                {
                    var latestBoost = activeBoosts.FirstOrDefault(b => b.ProductID == p.ProductID);
                    var revenueInRange = deliveredOrderItemsInRange
                        .Where(oi => oi.ProductID == p.ProductID)
                        .Sum(oi => oi.TotalPrice);
                    var unitsInRange = deliveredOrderItemsInRange
                        .Where(oi => oi.ProductID == p.ProductID)
                        .Sum(oi => oi.Quantity);

                    return new BoostPerformanceViewModel
                    {
                        ProductID = p.ProductID,
                        ProductName = p.ProductName,
                        IsCurrentlyBoosted = latestBoost != null && latestBoost.Status == "Active" && latestBoost.EndDate > now,
                        LastBoostStatus = latestBoost?.Status ?? "Expired",
                        RevenueInRange = revenueInRange,
                        UnitsSoldInRange = unitsInRange
                    };
                })
                .OrderByDescending(x => x.IsCurrentlyBoosted)
                .ThenByDescending(x => x.RevenueInRange)
                .Take(6)
                .ToList();
        }

        private (DateTime StartDate, DateTime EndDate) GetDateRange()
        {
            var today = DateTime.Today;
            var startDate = today.AddDays(-30);
            var endDate = today.AddDays(1).AddTicks(-1);

            if (Range == "7")
            {
                startDate = today.AddDays(-7);
            }
            else if (Range == "30")
            {
                startDate = today.AddDays(-30);
            }
            else if (Range == "90")
            {
                startDate = today.AddDays(-90);
            }
            else if (Range == "custom")
            {
                if (FromDate.HasValue)
                {
                    startDate = FromDate.Value.Date;
                }

                if (ToDate.HasValue)
                {
                    endDate = ToDate.Value.Date.AddDays(1).AddTicks(-1);
                }

                if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
                {
                    startDate = ToDate.Value.Date;
                    endDate = FromDate.Value.Date.AddDays(1).AddTicks(-1);
                }
            }

            return (startDate, endDate);
        }

        private static bool IsPending(string? status)
        {
            return string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreparing(string? status)
        {
            return string.Equals(status, "Preparing", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOutForDelivery(string? status)
        {
            return string.Equals(status, "Out for Delivery", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDelivered(string? status)
        {
            return string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCancelled(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status.Contains("Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActiveOrder(string? status)
        {
            return !IsDelivered(status) && !IsCancelled(status);
        }
    }

    public class BestSellingProductViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int QuantitySold { get; set; }

        public decimal Revenue { get; set; }
    }

    public class DailyRevenueViewModel
    {
        public DateTime Date { get; set; }

        public decimal Revenue { get; set; }

        public int Orders { get; set; }

        public int ItemsSold { get; set; }
    }

    public class RecentOrderViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    // NEW
    public class BoostPerformanceViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public bool IsCurrentlyBoosted { get; set; }
        public string LastBoostStatus { get; set; } = string.Empty;
        public decimal RevenueInRange { get; set; }
        public int UnitsSoldInRange { get; set; }
    }
}