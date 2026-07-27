using Microsoft.AspNetCore.Authorization;
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

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            ILogger<IndexModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _logger = logger;
        }

        // Single source of truth for valid order statuses — used for server-side
        // validation AND to build both dropdowns in the view, so the two can never
        // drift out of sync with each other or with what's actually accepted here.
        public static readonly string[] ValidOrderStatuses =
        {
            "Pending", "Confirmed", "Preparing", "OutForDelivery", "Delivered", "Cancelled"
        };

        public List<OrderViewModel> Orders { get; set; } = new();
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; }
        public string StatusFilter { get; set; } = string.Empty;
        public string SearchTerm { get; set; } = string.Empty;
        private const int PageSize = 10;

        public async Task<IActionResult> OnGetAsync(int pageIndex = 1, string statusFilter = "", string searchTerm = "")
        {
            try
            {
                // 1. Check store owner
                if (!await _currentStoreService.IsStoreOwnerAsync())
                    return RedirectToPage("/Account/AccessDenied");

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found.";
                    return Page();
                }

                PageIndex = pageIndex < 1 ? 1 : pageIndex;
                StatusFilter = statusFilter ?? string.Empty;
                SearchTerm = searchTerm ?? string.Empty;

                // 2. Get all order IDs that contain items from this store
                var orderIdsQuery = _context.OrderItems
                    .Where(oi => oi.StoreID == store.StoreID)
                    .Select(oi => oi.OrderID)
                    .Distinct();

                var ordersQuery = _context.Orders
                    .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                    .Where(o => orderIdsQuery.Contains(o.OrderID));

                // 3. Apply filters
                if (!string.IsNullOrEmpty(StatusFilter))
                    ordersQuery = ordersQuery.Where(o => o.Status == StatusFilter);

                if (!string.IsNullOrEmpty(SearchTerm))
                    ordersQuery = ordersQuery.Where(o =>
                        o.OrderNumber.Contains(SearchTerm) ||
                        o.Customer.User.FullName.Contains(SearchTerm));

                // 4. Pagination
                var totalCount = await ordersQuery.CountAsync();
                TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

                // Guard against requesting a page past the end (e.g. after a filter
                // shrinks the result set while the user was on a later page).
                if (PageIndex > TotalPages) PageIndex = TotalPages;

                var orders = await ordersQuery
                    .OrderByDescending(o => o.OrderDate)
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // 5. Build ViewModel with item counts and multi-vendor flag
                // BUGFIX (perf): previously issued one COUNT query per order in a
                // loop (N+1). Batched into a single grouped query for the whole page.
                var orderIds = orders.Select(o => o.OrderID).ToList();

                var itemCounts = await _context.OrderItems
                    .Where(oi => orderIds.Contains(oi.OrderID) && oi.StoreID == store.StoreID)
                    .GroupBy(oi => oi.OrderID)
                    .Select(g => new { OrderID = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.OrderID, x => x.Count);

                // Fix for H4 (partial, Option 1): does each order contain items
                // from more than one store? If so, Order.Status is shared with
                // other stores and the view must not let this store owner end
                // the whole order (Delivered/Cancelled). Batched the same way as
                // itemCounts above so this page never issues a per-order query.
                var storeCountsPerOrder = await _context.OrderItems
                    .Where(oi => orderIds.Contains(oi.OrderID))
                    .GroupBy(oi => oi.OrderID)
                    .Select(g => new { OrderID = g.Key, StoreCount = g.Select(x => x.StoreID).Distinct().Count() })
                    .ToDictionaryAsync(x => x.OrderID, x => x.StoreCount);

                Orders = orders.Select(order => new OrderViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    CustomerName = order.Customer?.User?.FullName ?? "Customer",
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    OrderDate = order.OrderDate,
                    ItemCount = itemCounts.TryGetValue(order.OrderID, out var count) ? count : 0,
                    IsMultiVendor = storeCountsPerOrder.TryGetValue(order.OrderID, out var storeCount) && storeCount > 1
                }).ToList();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Store Owner Orders index.");
                TempData["ErrorMessage"] = "Something went wrong while loading your orders. Please try again.";
                Orders = new List<OrderViewModel>();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(
            int orderId,
            string newStatus,
            int pageIndex = 1,
            string statusFilter = "",
            string searchTerm = "")
        {
            // BUGFIX: previously redirected using this.PageIndex/StatusFilter/SearchTerm,
            // which are plain properties only ever set inside OnGetAsync — on a POST
            // they were always at their default (page 1, no filters), so updating a
            // status from page 2+ (once pagination is reachable at all) silently
            // bounced the user back to page 1 with filters cleared. Now bound directly
            // from hidden fields in the update form and used for every redirect below.
            var routeValues = new { pageIndex, statusFilter, searchTerm };

            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found.";
                return RedirectToPage(routeValues);
            }

            // BUGFIX: newStatus was previously written straight to the database with
            // no validation at all. The <select> in the UI only offers valid values,
            // but that's not a security boundary — a crafted POST could persist any
            // string, silently breaking every exact-match status comparison
            // elsewhere on this page (and likely elsewhere in the app).
            if (string.IsNullOrWhiteSpace(newStatus) ||
                !ValidOrderStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "That isn't a valid order status.";
                return RedirectToPage(routeValues);
            }

            try
            {
                // Verify order belongs to this store
                var hasStoreItem = await _context.OrderItems
                    .AnyAsync(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID);

                if (!hasStoreItem)
                {
                    TempData["ErrorMessage"] = "Unauthorized access to this order.";
                    return RedirectToPage(routeValues);
                }

                // Switched from FindAsync to a query with Include so we have
                // order.Customer.UserID available for the notification below.
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.OrderID == orderId);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToPage(routeValues);
                }

                // Fix for H4 (partial, Option 1): if this order has items from
                // more than one store, refuse to let this store owner set a
                // terminal status (Delivered/Cancelled) — Order.Status is a
                // single, shared field, so ending it here would also end it
                // for every other store still involved in this order.
                if (AllowedOrderStatuses.IsTerminal(newStatus))
                {
                    var distinctStoreCount = await _context.OrderItems
                        .Where(oi => oi.OrderID == orderId)
                        .Select(oi => oi.StoreID)
                        .Distinct()
                        .CountAsync();

                    if (distinctStoreCount > 1)
                    {
                        TempData["ErrorMessage"] =
                            "This order includes items from other stores. " +
                            "'" + newStatus + "' cannot be set from here for a multi-vendor order.";

                        return RedirectToPage(routeValues);
                    }
                }

                var previousStatus = order.Status;
                order.Status = newStatus;

                // =====================================================
                // NOTIFY CUSTOMER — this was the missing piece. A customer
                // previously had no way to know their order moved to
                // Confirmed/Preparing/OutForDelivery/Delivered/Cancelled
                // unless they manually refreshed their order page.
                // =====================================================
                if (order.Customer != null &&
                    !string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase))
                {
                    var message = newStatus switch
                    {
                        "Confirmed" => $"Your order {order.OrderNumber} has been confirmed by the store.",
                        "Preparing" => $"Your order {order.OrderNumber} is being prepared.",
                        "OutForDelivery" => $"Your order {order.OrderNumber} is out for delivery.",
                        "Delivered" => $"Your order {order.OrderNumber} has been delivered. Enjoy!",
                        "Cancelled" => $"Your order {order.OrderNumber} has been cancelled.",
                        _ => $"Your order {order.OrderNumber} status changed to {newStatus}."
                    };

                    _context.Notifications.Add(new Notification
                    {
                        UserID = order.Customer.UserID,
                        Title = "Order status updated",
                        Message = message,
                        Type = "OrderStatus",
                        ReferenceID = order.OrderID,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        SentVia = "System"
                    });
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to {newStatus}.";
                return RedirectToPage(routeValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for Order {OrderId}.", orderId);
                TempData["ErrorMessage"] = "Something went wrong while updating the order. Please try again.";
                return RedirectToPage(routeValues);
            }
        }
    }

    public class OrderViewModel
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public int ItemCount { get; set; }
        public bool IsMultiVendor { get; set; }
    }
}