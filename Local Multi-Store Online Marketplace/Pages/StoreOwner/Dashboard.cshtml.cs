#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using System.Linq;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly UserManager<User> _userManager;
        

        public DashboardModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            UserManager<User> userManager)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _userManager = userManager;
        }

        public Store Store { get; set; } = new();
        public DashboardStats Stats { get; set; } = new();
        public List<RecentOrder> RecentOrders { get; set; } = new();
        public List<TopProduct> TopProducts { get; set; } = new();
        public List<LowStockProduct> LowStockProducts { get; set; } = new();
        public List<SalesDataPoint> WeeklySales { get; set; } = new();
        
        public async Task<IActionResult> OnGetAsync()
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

            ViewData["StoreName"] = store.StoreName;
            ViewData["StoreId"] = store.StoreID;

            await LoadDashboardStats(store.StoreID);
            await LoadRecentOrders(store.StoreID);
            await LoadTopProducts(store.StoreID);
            await LoadLowStockProducts(store.StoreID);
            await LoadWeeklySales(store.StoreID);

            return Page();
        }

        private async Task LoadDashboardStats(int storeId)
        {
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);
            var store = await _context.Stores
    .FirstAsync(s => s.StoreID == storeId);

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
            // PROFIT ANALYTICS (NEW)
            // =========================

            var deliveredOrderItems = await _context.OrderItems
                .Include(oi => oi.Product)
                .Where(oi =>
                    oi.StoreID == storeId &&
                    oi.Order != null &&
                    oi.Order.Status == "Delivered")
                .ToListAsync();

            Stats.TotalProfit = deliveredOrderItems
                .Where(oi =>
                    oi.Product != null &&
                    oi.Product.OriginalPrice.HasValue)
                .Sum(oi =>
                    (oi.Product.Price - oi.Product.OriginalPrice.Value) * oi.Quantity);

            var productsWithMargin = await _context.Products
                .Where(p =>
                    p.StoreID == storeId &&
                    p.OriginalPrice.HasValue &&
                    p.OriginalPrice > 0)
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
                    ((p.Price - p.OriginalPrice.Value) / p.Price) * 100 < 10);
        }

        private async Task LoadRecentOrders(int storeId)
        {
            RecentOrders = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Customer)
                        .ThenInclude(c => c.User)
                .Where(oi => oi.StoreID == storeId && oi.Order != null)
                .GroupBy(oi => new
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
                .ToListAsync();
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

        // NEW FIELDS (PROFIT ANALYTICS)
        public decimal TotalProfit { get; set; }
        public decimal AverageMarginPercent { get; set; }
        public int LowMarginProductsCount { get; set; }
        public decimal OutstandingBalance { get; set; }

        public string SubscriptionStatus { get; set; } = "";
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
}