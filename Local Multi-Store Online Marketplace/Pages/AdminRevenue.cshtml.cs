using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class AdminRevenueModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public AdminRevenueModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─── Summary properties ───
        public decimal TotalPlatformRevenue { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalSubscriptionRevenue { get; set; }
        public decimal TotalBoostRevenue { get; set; }   // ADD THIS
        public int TotalStoresWithRevenue { get; set; }
        public decimal TotalSales { get; set; } // total order amount before commission

        // ─── Filter properties ───
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }
        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }
        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)]
        public string RevenueType { get; set; } = "All"; // All, Commission, Subscription, Boost

        // ─── Pagination ───
        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        // ─── Data ───
        public List<StoreRevenueDto> StoreRevenueList { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Normalise dates
            if (StartDate.HasValue)
                StartDate = StartDate.Value.Date;
            if (EndDate.HasValue)
                EndDate = EndDate.Value.Date.AddDays(1).AddSeconds(-1);

            // ─── 1. Build base store query with search filter ───
            var storesQuery = _context.Stores.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                storesQuery = storesQuery.Where(s => s.StoreName.Contains(SearchTerm));

            TotalCount = await storesQuery.CountAsync();

            // ─── 2. Get paginated store data ───
            var storePage = await storesQuery
                .OrderBy(s => s.StoreName)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(s => new { s.StoreID, s.StoreName, s.CommissionRate })
                .ToListAsync();

            var storeIds = storePage.Select(s => s.StoreID).ToList();

            // ─── 3. Commission & sales data for these stores ───
            var orderDataQuery = from oi in _context.OrderItems
                                 where storeIds.Contains(oi.StoreID)
                                    && oi.Order.Status == "Delivered"
                                    && (StartDate == null || oi.Order.OrderDate >= StartDate)
                                    && (EndDate == null || oi.Order.OrderDate <= EndDate)
                                 group oi by oi.StoreID into g
                                 select new
                                 {
                                     StoreId = g.Key,
                                     TotalSales = g.Sum(oi => oi.TotalPrice),
                                     OrderCount = g.Select(oi => oi.OrderID).Distinct().Count()
                                 };

            var orderData = await orderDataQuery.ToDictionaryAsync(
                x => x.StoreId,
                x => new { x.TotalSales, x.OrderCount }
            );

            // ─── 4. Subscription data for these stores ───
            var subscriptionQuery = from sp in _context.SubscriptionPayments
                                    where storeIds.Contains(sp.StoreId)
                                       && (StartDate == null || sp.PaymentDate >= StartDate)
                                       && (EndDate == null || sp.PaymentDate <= EndDate)
                                    group sp by sp.StoreId into g
                                    select new
                                    {
                                        StoreId = g.Key,
                                        SubscriptionFees = g.Sum(sp => sp.Amount)
                                    };

            var subscriptionData = await subscriptionQuery.ToDictionaryAsync(
                x => x.StoreId,
                x => x.SubscriptionFees
            );

            // ─── 4b. Boost revenue for these stores (NEW) ───
            // Counted the same way as your other revenue lines: booked once a boost
            // has been paid for (Active or Expired), matching AmountPaid at time of purchase.
            var boostQuery = from b in _context.ProductBoosts
                             where storeIds.Contains(b.StoreID)
                                && (b.Status == "Active" || b.Status == "Expired")
                                && (StartDate == null || b.CreatedAt >= StartDate)
                                && (EndDate == null || b.CreatedAt <= EndDate)
                             group b by b.StoreID into g
                             select new
                             {
                                 StoreId = g.Key,
                                 BoostFees = g.Sum(b => b.AmountPaid)
                             };

            var boostData = await boostQuery.ToDictionaryAsync(
                x => x.StoreId,
                x => x.BoostFees
            );

            // ─── 5. Build final list ───
            var revenueList = new List<StoreRevenueDto>();

            foreach (var store in storePage)
            {
                decimal sales = 0;
                int orderCount = 0;

                if (orderData.TryGetValue(store.StoreID, out var ord))
                {
                    sales = ord.TotalSales;
                    orderCount = ord.OrderCount;
                }

                decimal commission = sales * store.CommissionRate / 100m;
                decimal subscription = subscriptionData.TryGetValue(store.StoreID, out var sub) ? sub : 0;
                decimal boostFees = boostData.TryGetValue(store.StoreID, out var bf) ? bf : 0;   // ADD THIS

                revenueList.Add(new StoreRevenueDto
                {
                    StoreName = store.StoreName,
                    CommissionRate = store.CommissionRate,
                    TotalSales = sales,
                    Commission = commission,
                    SubscriptionFees = subscription,
                    BoostFees = boostFees,   // ADD THIS
                    OrderCount = orderCount
                });
            }

            // ─── 6. Apply revenue type filter (in memory) ───
            if (RevenueType == "Commission")
                revenueList = revenueList.Where(r => r.Commission > 0).ToList();
            else if (RevenueType == "Subscription")
                revenueList = revenueList.Where(r => r.SubscriptionFees > 0).ToList();
            else if (RevenueType == "Boost")   // ADD THIS
                revenueList = revenueList.Where(r => r.BoostFees > 0).ToList();

            StoreRevenueList = revenueList;

            // ─── 7. Summary totals (all stores, not paginated) ───
            var allStoreIds = await storesQuery.Select(s => s.StoreID).ToListAsync();

            // Total sales & commission
            var totalOrderData = await _context.OrderItems
                .Where(oi => allStoreIds.Contains(oi.StoreID)
                    && oi.Order.Status == "Delivered"
                    && (StartDate == null || oi.Order.OrderDate >= StartDate)
                    && (EndDate == null || oi.Order.OrderDate <= EndDate))
                .GroupBy(oi => oi.StoreID)
                .Select(g => new
                {
                    StoreId = g.Key,
                    TotalSales = g.Sum(oi => oi.TotalPrice)
                })
                .ToListAsync();

            // Get commission rates for these stores
            var rates = await _context.Stores
                .Where(s => allStoreIds.Contains(s.StoreID))
                .ToDictionaryAsync(s => s.StoreID, s => s.CommissionRate);

            TotalSales = totalOrderData.Sum(x => x.TotalSales);
            TotalCommission = totalOrderData.Sum(x => x.TotalSales * rates.GetValueOrDefault(x.StoreId, 0) / 100m);

            // Total subscription
            var totalSubData = await _context.SubscriptionPayments
                .Where(sp => allStoreIds.Contains(sp.StoreId)
                    && (StartDate == null || sp.PaymentDate >= StartDate)
                    && (EndDate == null || sp.PaymentDate <= EndDate))
                .SumAsync(sp => sp.Amount);
            TotalSubscriptionRevenue = totalSubData;

            // Total boost revenue (NEW)
            TotalBoostRevenue = await _context.ProductBoosts
                .Where(b => allStoreIds.Contains(b.StoreID)
                    && (b.Status == "Active" || b.Status == "Expired")
                    && (StartDate == null || b.CreatedAt >= StartDate)
                    && (EndDate == null || b.CreatedAt <= EndDate))
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0m;

            TotalPlatformRevenue = TotalCommission + TotalSubscriptionRevenue + TotalBoostRevenue;   // CHANGED

            // Stores with revenue
            var storesWithRevenue = await _context.Stores
                .Where(s => allStoreIds.Contains(s.StoreID))
                .Select(s => new
                {
                    s.StoreID,
                    HasCommission = s.OrderItems.Any(oi => oi.Order.Status == "Delivered"
                        && (StartDate == null || oi.Order.OrderDate >= StartDate)
                        && (EndDate == null || oi.Order.OrderDate <= EndDate)),
                    HasSubscription = _context.SubscriptionPayments
                        .Any(sp => sp.StoreId == s.StoreID
                            && (StartDate == null || sp.PaymentDate >= StartDate)
                            && (EndDate == null || sp.PaymentDate <= EndDate)),
                    HasBoost = _context.ProductBoosts   // ADD THIS
                        .Any(b => b.StoreID == s.StoreID
                            && (b.Status == "Active" || b.Status == "Expired")
                            && (StartDate == null || b.CreatedAt >= StartDate)
                            && (EndDate == null || b.CreatedAt <= EndDate))
                })
                .ToListAsync();

            TotalStoresWithRevenue = storesWithRevenue.Count(s => s.HasCommission || s.HasSubscription || s.HasBoost);   // CHANGED
        }

        // ─── CSV Export ───
        public async Task<IActionResult> OnGetExportCsvAsync(
            DateTime? startDate, DateTime? endDate, string searchTerm, string revenueType)
        {
            var storesQuery = _context.Stores.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
                storesQuery = storesQuery.Where(s => s.StoreName.Contains(searchTerm));

            var allStores = await storesQuery
                .Select(s => new { s.StoreID, s.StoreName, s.CommissionRate })
                .ToListAsync();

            var storeIds = allStores.Select(s => s.StoreID).ToList();

            // Order data
            var orderData = await _context.OrderItems
                .Where(oi => storeIds.Contains(oi.StoreID)
                    && oi.Order.Status == "Delivered"
                    && (startDate == null || oi.Order.OrderDate >= startDate)
                    && (endDate == null || oi.Order.OrderDate <= endDate))
                .GroupBy(oi => oi.StoreID)
                .Select(g => new
                {
                    StoreId = g.Key,
                    TotalSales = g.Sum(oi => oi.TotalPrice)
                })
                .ToDictionaryAsync(x => x.StoreId, x => x.TotalSales);

            // Subscription data
            var subData = await _context.SubscriptionPayments
                .Where(sp => storeIds.Contains(sp.StoreId)
                    && (startDate == null || sp.PaymentDate >= startDate)
                    && (endDate == null || sp.PaymentDate <= endDate))
                .GroupBy(sp => sp.StoreId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    Total = g.Sum(sp => sp.Amount)
                })
                .ToDictionaryAsync(x => x.StoreId, x => x.Total);

            // Boost data (NEW)
            var boostData = await _context.ProductBoosts
                .Where(b => storeIds.Contains(b.StoreID)
                    && (b.Status == "Active" || b.Status == "Expired")
                    && (startDate == null || b.CreatedAt >= startDate)
                    && (endDate == null || b.CreatedAt <= endDate))
                .GroupBy(b => b.StoreID)
                .Select(g => new
                {
                    StoreId = g.Key,
                    Total = g.Sum(b => b.AmountPaid)
                })
                .ToDictionaryAsync(x => x.StoreId, x => x.Total);

            var data = new List<StoreRevenueDto>();

            foreach (var store in allStores)
            {
                decimal sales = orderData.TryGetValue(store.StoreID, out var s) ? s : 0;
                decimal commission = sales * store.CommissionRate / 100m;
                decimal subscription = subData.TryGetValue(store.StoreID, out var sub) ? sub : 0;
                decimal boostFees = boostData.TryGetValue(store.StoreID, out var bf) ? bf : 0;   // ADD THIS

                if (revenueType == "Commission" && commission == 0) continue;
                if (revenueType == "Subscription" && subscription == 0) continue;
                if (revenueType == "Boost" && boostFees == 0) continue;   // ADD THIS

                data.Add(new StoreRevenueDto
                {
                    StoreName = store.StoreName,
                    CommissionRate = store.CommissionRate,
                    TotalSales = sales,
                    Commission = commission,
                    SubscriptionFees = subscription,
                    BoostFees = boostFees,   // ADD THIS
                    OrderCount = 0
                });
            }

            data = data.OrderByDescending(r => r.Commission + r.SubscriptionFees + r.BoostFees).ToList();   // CHANGED

            // Build CSV
            var csv = "Store Name,Commission Rate,Total Sales,Commission,Subscription Fees,Boost Fees,Total Revenue\n";   // CHANGED
            foreach (var item in data)
            {
                var total = item.Commission + item.SubscriptionFees + item.BoostFees;   // CHANGED
                csv += $"{item.StoreName},{item.CommissionRate}%,{item.TotalSales:N2},{item.Commission:N2},{item.SubscriptionFees:N2},{item.BoostFees:N2},{total:N2}\n";   // CHANGED
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", $"RevenueReport_{DateTime.Now:yyyyMMdd}.csv");
        }
    }

    // ─── DTO ───
    public class StoreRevenueDto
    {
        public string StoreName { get; set; } = string.Empty;
        public decimal CommissionRate { get; set; }
        public decimal TotalSales { get; set; }
        public decimal Commission { get; set; }
        public decimal SubscriptionFees { get; set; }
        public decimal BoostFees { get; set; }   // ADD THIS
        public int OrderCount { get; set; }
        public decimal TotalRevenue => Commission + SubscriptionFees + BoostFees;   // CHANGED
    }
}