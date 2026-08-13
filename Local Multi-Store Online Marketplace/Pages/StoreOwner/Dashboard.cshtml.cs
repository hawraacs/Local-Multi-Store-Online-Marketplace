#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using System.Linq;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly UserManager<User> _userManager;
        private readonly BoostManager _boostManager;   // ADD THIS

        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
    ApplicationDbContext context,
    ICurrentStoreService currentStoreService,
    UserManager<User> userManager,
    IConfiguration configuration,
    BoostManager boostManager,                  // ADD THIS
    ILogger<DashboardModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _userManager = userManager;
            _configuration = configuration;
            _boostManager = boostManager;                // ADD THIS
            _logger = logger;
        }

        public Store Store { get; set; } = new();
        public DashboardStats Stats { get; set; } = new();
        public List<RecentOrder> RecentOrders { get; set; } = new();
        public List<TopProduct> TopProducts { get; set; } = new();
        public List<LowStockProduct> LowStockProducts { get; set; } = new();
        public List<SalesDataPoint> WeeklySales { get; set; } = new();
        public decimal StripeBalance { get; set; } = 0;

        // NEW — active/pending boosts for this store, shown in a small panel
        public List<ActiveBoostSummary> ActiveBoosts { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                    return RedirectToPage("/Account/Login");

                var store = await _currentStoreService.GetCurrentStoreAsync();

                if (store == null)
                {
                    store = await _context.Stores
                        .FirstOrDefaultAsync(s =>
                            s.OwnerUserID == user.Id &&
                            s.Status == "Approved");
                }

                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store profile was not found. Please contact admin.";
                    Store = new Store { StoreName = "No Store Found" };
                    return Page();
                }

                Store = store;
                if (!string.IsNullOrWhiteSpace(store.StripeAccountId))
                {
                    StripeBalance = await GetStripeBalanceAsync(store.StripeAccountId);
                }

                ViewData["StoreName"] = store.StoreName;
                ViewData["StoreId"] = store.StoreID;

                await LoadDashboardStats(store);
                await LoadRecentOrders(store.StoreID);
                await LoadTopProducts(store.StoreID);
                await LoadLowStockProducts(store.StoreID);
                await LoadWeeklySales(store.StoreID);
                await LoadBoostSummary(store.StoreID);   // ADD THIS

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Store Owner Dashboard for user {UserId}.", User?.Identity?.Name);

                // Keep whatever defaults are already in place so the page still renders
                // (empty lists, zeroed stats) instead of showing an unhandled-exception page.
                TempData["ErrorMessage"] = "Something went wrong while loading your dashboard. Please refresh the page or try again shortly.";

                if (string.IsNullOrWhiteSpace(Store?.StoreName))
                {
                    Store = new Store { StoreName = "Dashboard" };
                }

                return Page();
            }
        }

        private async Task LoadDashboardStats(Store store)
        {
            var storeId = store.StoreID;
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);

            Stats.OutstandingBalance = store.OutstandingBalance;
            Stats.SubscriptionStatus = store.SubscriptionStatus;

            var orderItemsQuery = _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.StoreID == storeId);

            Stats.TotalProducts = await _context.Products
                .CountAsync(p => p.StoreID == storeId);

            Stats.TotalOrders = await orderItemsQuery
                .Select(oi => oi.OrderID)
                .Distinct()
                .CountAsync();

            Stats.TotalRevenue = await orderItemsQuery
                .Where(oi => oi.Order != null && oi.Order.Status == "Delivered")
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0;

            Stats.AverageRating = await _context.Reviews
                .Where(r => r.StoreID == storeId)
                .AverageAsync(r => (decimal?)r.Rating) ?? 0;

            Stats.TodayOrders = await orderItemsQuery
                .Where(oi => oi.Order != null && oi.Order.OrderDate.Date == today)
                .Select(oi => oi.OrderID)
                .Distinct()
                .CountAsync();

            Stats.TodayRevenue = await orderItemsQuery
                .Where(oi =>
                    oi.Order != null &&
                    oi.Order.Status == "Delivered" &&
                    oi.Order.OrderDate.Date == today)
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0;

            Stats.PendingOrders = await orderItemsQuery
                .Where(oi =>
                    oi.Order != null &&
                    (oi.Order.Status == "Pending" ||
                     oi.Order.Status == "Pending Confirmation"))
                .Select(oi => oi.OrderID)
                .Distinct()
                .CountAsync();

            Stats.PreparingOrders = await orderItemsQuery
                .Where(oi => oi.Order != null && oi.Order.Status == "Preparing")
                .Select(oi => oi.OrderID)
                .Distinct()
                .CountAsync();

            Stats.OutForDeliveryOrders = await orderItemsQuery
                .Where(oi =>
                    oi.Order != null &&
                    (oi.Order.Status == "OutForDelivery" ||
                     oi.Order.Status == "Out for Delivery"))
                .Select(oi => oi.OrderID)
                .Distinct()
                .CountAsync();

            var thisWeekRevenue = await orderItemsQuery
                .Where(oi =>
                    oi.Order != null &&
                    oi.Order.Status == "Delivered" &&
                    oi.Order.OrderDate >= weekAgo)
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0;

            var lastWeekRevenue = await orderItemsQuery
                .Where(oi =>
                    oi.Order != null &&
                    oi.Order.Status == "Delivered" &&
                    oi.Order.OrderDate >= weekAgo.AddDays(-7) &&
                    oi.Order.OrderDate < weekAgo)
                .SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0;

            Stats.RevenueGrowth = lastWeekRevenue > 0
                ? ((thisWeekRevenue - lastWeekRevenue) / lastWeekRevenue) * 100
                : (thisWeekRevenue > 0 ? 100 : 0);

            Stats.LowStockCount = await _context.Products
                .CountAsync(p =>
                    p.StoreID == storeId &&
                    p.Quantity <= p.LowStockThreshold &&
                    p.Quantity > 0);

            Stats.OutOfStockCount = await _context.Products
                .CountAsync(p =>
                    p.StoreID == storeId &&
                    p.Quantity <= 0);

            // =========================
            // PROFIT ANALYTICS
            // =========================

            var deliveredOrderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Where(oi =>
                    oi.StoreID == storeId &&
                    oi.Order != null &&
                    oi.Order.Status == "Delivered")
                .ToListAsync();

            // BUGFIX: this used to be (Product.Price - Product.OriginalPrice) * Quantity,
            // i.e. entirely today's catalog prices. That meant profit on a past
            // delivered order would silently change any time the product's price
            // was edited later. oi.TotalPrice is what was actually charged and is
            // locked in at order time, so we use that for the revenue side and only
            // fall back to the current Product.OriginalPrice for cost (the schema
            // doesn't capture a historical cost snapshot per order item — if one is
            // added later, swap it in here for a fully historical figure).
            Stats.TotalProfit = deliveredOrderItems
                .Where(oi =>
                    oi.Product != null &&
                    oi.Product.OriginalPrice.HasValue)
                .Sum(oi =>
                    oi.TotalPrice - (oi.Product.OriginalPrice.Value * oi.Quantity));

            // BUGFIX: previously this filtered on OriginalPrice > 0 but not Price > 0.
            // A product with Price == 0 (e.g. a free/giveaway item) would divide by
            // zero below and throw DivideByZeroException for the whole dashboard load.
            var productsWithMargin = await _context.Products
                .Where(p =>
                    p.StoreID == storeId &&
                    p.OriginalPrice.HasValue &&
                    p.OriginalPrice > 0 &&
                    p.Price > 0)
                .ToListAsync();

            if (productsWithMargin.Any())
            {
                Stats.AverageMarginPercent = productsWithMargin
                    .Average(p =>
                        ((p.Price - p.OriginalPrice.Value) / p.Price) * 100);
            }

            Stats.LowMarginProductsCount = await _context.Products
                .CountAsync(p =>
                    p.StoreID == storeId &&
                    p.OriginalPrice.HasValue &&
                    p.OriginalPrice > 0 &&
                    p.Price > 0 &&
                    ((p.Price - p.OriginalPrice.Value) / p.Price) * 100 < 10);

            // =========================
            // BOOST STATS (NEW)
            // =========================

            await _boostManager.ExpireDueBoostsAsync();

            Stats.ActiveBoostsCount = await _context.ProductBoosts
                .CountAsync(b => b.StoreID == storeId && b.Status == "Active");

            Stats.TotalBoostSpend = await _context.ProductBoosts
                .Where(b => b.StoreID == storeId && (b.Status == "Active" || b.Status == "Expired"))
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0m;
        }

        // NEW — small panel of the store's currently active/pending boosts
        private async Task LoadBoostSummary(int storeId)
        {
            ActiveBoosts = await _context.ProductBoosts
                .Include(b => b.Product)
                .Where(b => b.StoreID == storeId && (b.Status == "Active" || b.Status == "PendingPayment"))
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .Select(b => new ActiveBoostSummary
                {
                    ProductID = b.ProductID,
                    ProductName = b.Product != null ? b.Product.ProductName : "Product",
                    Status = b.Status,
                    EndDate = b.EndDate,
                    DurationDays = b.DurationDays
                })
                .ToListAsync();
        }

        private async Task LoadRecentOrders(int storeId)
        {
            // BUGFIX: grouping directly in the EF query on a key built from a
            // three-level navigation chain (Order.Customer.User.FullName) wrapped
            // in a ternary is fragile to translate to SQL and can throw at runtime
            // depending on the EF Core version/provider. Pull the flat rows down
            // first, then group and shape them in memory instead.
            var rows = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.User)
                .Where(oi => oi.StoreID == storeId && oi.Order != null)
                .Select(oi => new
                {
                    oi.OrderID,
                    oi.Order.OrderNumber,
                    CustomerName = oi.Order.Customer != null && oi.Order.Customer.User != null
                        ? oi.Order.Customer.User.FullName
                        : "Customer",
                    oi.Order.TotalAmount,
                    oi.Order.Status,
                    oi.Order.OrderDate
                })
                .ToListAsync();

            RecentOrders = rows
                .GroupBy(r => new { r.OrderID, r.OrderNumber, r.CustomerName, r.TotalAmount, r.Status, r.OrderDate })
                .Select(g => new RecentOrder
                {
                    OrderID = g.Key.OrderID,
                    OrderNumber = g.Key.OrderNumber,
                    CustomerName = g.Key.CustomerName,
                    TotalAmount = g.Key.TotalAmount,
                    Status = g.Key.Status,
                    OrderDate = g.Key.OrderDate,
                    ItemCount = g.Count()
                })
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToList();
        }

        private async Task LoadTopProducts(int storeId)
        {
            TopProducts = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi =>
                    oi.StoreID == storeId &&
                    oi.Order != null &&
                    oi.Order.Status == "Delivered")
                .GroupBy(oi => new
                {
                    oi.ProductID,
                    oi.ProductName
                })
                .Select(g => new TopProduct
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.TotalPrice)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(5)
                .ToListAsync();
        }

        private async Task LoadLowStockProducts(int storeId)
        {
            LowStockProducts = await _context.Products
                .Where(p =>
                    p.StoreID == storeId &&
                    p.Quantity <= p.LowStockThreshold)
                .Select(p => new LowStockProduct
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    CurrentStock = p.Quantity,
                    LowStockThreshold = p.LowStockThreshold
                })
                .OrderBy(p => p.CurrentStock)
                .Take(10)
                .ToListAsync();
        }

        private async Task LoadWeeklySales(int storeId)
        {
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.UtcNow.Date.AddDays(-i))
                .OrderBy(d => d)
                .ToList();

            var salesData = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi =>
                    oi.StoreID == storeId &&
                    oi.Order != null &&
                    oi.Order.Status == "Delivered")
                .GroupBy(oi => oi.Order.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(oi => oi.TotalPrice)
                })
                .ToListAsync();

            WeeklySales = last7Days.Select(d => new SalesDataPoint
            {
                Date = d,
                Revenue = salesData.FirstOrDefault(s => s.Date == d)?.Revenue ?? 0,
                DayName = d.ToString("ddd")
            }).ToList();
        }
        private async Task<decimal> GetStripeBalanceAsync(string stripeAccountId)
        {
            try
            {
                Stripe.StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var balanceService = new Stripe.BalanceService();

                var balance = await balanceService.GetAsync(
                    new Stripe.BalanceGetOptions(),
                    new Stripe.RequestOptions { StripeAccount = stripeAccountId }
                );

                var available = balance.Available.FirstOrDefault();

                return (available?.Amount ?? 0) / 100m;
            }
            catch (Exception ex)
            {
                // BUGFIX: previously this exception was silently swallowed, so a
                // misconfigured Stripe key or a revoked connected account would fail
                // forever with zero visibility. -1 is used as a sentinel the view
                // renders as "Unavailable" rather than a misleading "$-1.00".
                _logger.LogWarning(ex, "Failed to fetch Stripe balance for account {StripeAccountId}.", stripeAccountId);
                return -1;
            }
        }
    }

    public class DashboardStats
    {


        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRating { get; set; }
        public int TodayOrders { get; set; }
        public decimal TodayRevenue { get; set; }
        public int PendingOrders { get; set; }
        public int PreparingOrders { get; set; }
        public int OutForDeliveryOrders { get; set; }
        public decimal RevenueGrowth { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // PROFIT ANALYTICS
        public decimal TotalProfit { get; set; }
        public decimal AverageMarginPercent { get; set; }
        public int LowMarginProductsCount { get; set; }
        public decimal OutstandingBalance { get; set; }

        public string SubscriptionStatus { get; set; } = "";

        // BOOST STATS (NEW)
        public int ActiveBoostsCount { get; set; }
        public decimal TotalBoostSpend { get; set; }
    }

    public class RecentOrder
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public int ItemCount { get; set; }
    }

    public class TopProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class LowStockProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int LowStockThreshold { get; set; }
    }

    public class SalesDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public string DayName { get; set; } = string.Empty;
    }

    // NEW
    public class ActiveBoostSummary
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? EndDate { get; set; }
        public int DurationDays { get; set; }
    }
}
