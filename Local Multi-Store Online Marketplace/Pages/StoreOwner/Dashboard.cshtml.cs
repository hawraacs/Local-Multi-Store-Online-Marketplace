
    using global::Multi_Store.Core.Entities;
    using global::Multi_Store.Core.Interfaces;
    using global::Multi_Store.Infrastructure.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Entities;
    using Multi_Store.Core.Interfaces;
    using Multi_Store.Infrastructure.Data;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    public class DashboardModel : PageModel
        {
            private readonly ApplicationDbContext _context;
            private readonly ICurrentStoreService _currentStoreService;

            public DashboardModel(ApplicationDbContext context, ICurrentStoreService currentStoreService)
            {
                _context = context;
                _currentStoreService = currentStoreService;
            }

            public Store Store { get; set; } = new();
            public DashboardStats Stats { get; set; } = new();
            public List<RecentOrder> RecentOrders { get; set; } = new();
            public List<TopProduct> TopProducts { get; set; } = new();
            public List<LowStockProduct> LowStockProducts { get; set; } = new();
            public List<SalesDataPoint> WeeklySales { get; set; } = new();

            public async Task<IActionResult> OnGetAsync()
            {
                // Check if user is store owner
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    return RedirectToPage("/Account/AccessDenied");
                }

                var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found. Contact admin.";
                return Page();
            }

            Store = store;
            ViewData["StoreName"] = store.StoreName;

            // Load dashboard statistics
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
                var monthAgo = today.AddMonths(-1);

                // Get order items for this store
                var orderItemsQuery = _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.StoreID == storeId);

                // Total products
                Stats.TotalProducts = await _context.Products.CountAsync(p => p.StoreID == storeId);

                // Total orders
                Stats.TotalOrders = await orderItemsQuery.Select(oi => oi.OrderID).Distinct().CountAsync();

                // Total revenue
                Stats.TotalRevenue = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Delivered")
                    .SumAsync(oi => oi.TotalPrice);

                // Average rating
                Stats.AverageRating = await _context.Reviews
                    .Where(r => r.StoreID == storeId)
                    .AverageAsync(r => (decimal?)r.Rating) ?? 0;

                // Today's orders
                Stats.TodayOrders = await orderItemsQuery
                    .Where(oi => oi.Order.OrderDate.Date == today)
                    .Select(oi => oi.OrderID)
                    .Distinct()
                    .CountAsync();

                // Today's revenue
                Stats.TodayRevenue = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Delivered" && oi.Order.OrderDate.Date == today)
                    .SumAsync(oi => oi.TotalPrice);

                // Pending orders
                Stats.PendingOrders = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Pending" || oi.Order.Status == "Pending Confirmation")
                    .Select(oi => oi.OrderID)
                    .Distinct()
                    .CountAsync();

                // Preparing orders
                Stats.PreparingOrders = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Preparing")
                    .Select(oi => oi.OrderID)
                    .Distinct()
                    .CountAsync();

                // Out for delivery orders
                Stats.OutForDeliveryOrders = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "OutForDelivery")
                    .Select(oi => oi.OrderID)
                    .Distinct()
                    .CountAsync();

                // This week vs last week growth
                var thisWeekRevenue = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Delivered" && oi.Order.OrderDate >= weekAgo)
                    .SumAsync(oi => oi.TotalPrice);

                var lastWeekRevenue = await orderItemsQuery
                    .Where(oi => oi.Order.Status == "Delivered" &&
                        oi.Order.OrderDate >= weekAgo.AddDays(-7) &&
                        oi.Order.OrderDate < weekAgo)
                    .SumAsync(oi => oi.TotalPrice);

                Stats.RevenueGrowth = lastWeekRevenue > 0
                    ? ((thisWeekRevenue - lastWeekRevenue) / lastWeekRevenue) * 100
                    : (thisWeekRevenue > 0 ? 100 : 0);

                // Low stock count
                Stats.LowStockCount = await _context.Products
                    .CountAsync(p => p.StoreID == storeId && p.Quantity <= p.LowStockThreshold && p.Quantity > 0);

                // Out of stock count
                Stats.OutOfStockCount = await _context.Products
                    .CountAsync(p => p.StoreID == storeId && p.Quantity <= 0);
            }

            private async Task LoadRecentOrders(int storeId)
            {
                RecentOrders = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .ThenInclude(o => o.Customer)
                    .ThenInclude(c => c.User)
                    .Where(oi => oi.StoreID == storeId)
                    .Select(oi => new RecentOrder
                    {
                        OrderID = oi.OrderID,
                        OrderNumber = oi.Order.OrderNumber,
                        CustomerName = oi.Order.Customer.User.FullName,
                        TotalAmount = oi.Order.TotalAmount,
                        Status = oi.Order.Status,
                        OrderDate = oi.Order.OrderDate,
                        ItemCount = _context.OrderItems.Count(o => o.OrderID == oi.OrderID)
                    })
                    .Distinct()
                    .OrderByDescending(o => o.OrderDate)
                    .Take(10)
                    .ToListAsync();
            }

            private async Task LoadTopProducts(int storeId)
            {
                TopProducts = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Where(oi => oi.StoreID == storeId && oi.Order.Status == "Delivered")
                    .GroupBy(oi => new { oi.ProductID, oi.ProductName })
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
                    .Where(p => p.StoreID == storeId && p.Quantity <= p.LowStockThreshold)
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
                    .Where(oi => oi.StoreID == storeId && oi.Order.Status == "Delivered")
                    .GroupBy(oi => oi.Order.OrderDate.Date)
                    .Select(g => new { Date = g.Key, Revenue = g.Sum(oi => oi.TotalPrice) })
                    .ToListAsync();

                WeeklySales = last7Days.Select(d => new SalesDataPoint
                {
                    Date = d,
                    Revenue = salesData.FirstOrDefault(s => s.Date == d)?.Revenue ?? 0,
                    DayName = d.ToString("ddd")
                }).ToList();
            }
        }

        // Dashboard Statistics Class
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