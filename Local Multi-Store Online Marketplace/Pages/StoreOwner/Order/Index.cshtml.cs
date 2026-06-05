using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
 using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Interfaces;
    using Multi_Store.Infrastructure.Data;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
        {
            private readonly ApplicationDbContext _context;
            private readonly ICurrentStoreService _currentStoreService;

            public IndexModel(ApplicationDbContext context, ICurrentStoreService currentStoreService)
            {
                _context = context;
                _currentStoreService = currentStoreService;
            }

            public List<OrderViewModel> Orders { get; set; } = new();
            public int PageIndex { get; set; } = 1;
            public int TotalPages { get; set; }
            public string StatusFilter { get; set; } = string.Empty;
            public string SearchTerm { get; set; } = string.Empty;
            private const int PageSize = 10;

            public async Task<IActionResult> OnGetAsync(int pageIndex = 1, string statusFilter = "", string searchTerm = "")
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

                PageIndex = pageIndex;
                StatusFilter = statusFilter;
                SearchTerm = searchTerm;

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
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                var orders = await ordersQuery
                    .OrderByDescending(o => o.OrderDate)
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // 5. Build ViewModel with item counts
                Orders = new List<OrderViewModel>();
                foreach (var order in orders)
                {
                    var itemCount = await _context.OrderItems
                        .CountAsync(oi => oi.OrderID == order.OrderID && oi.StoreID == store.StoreID);

                    Orders.Add(new OrderViewModel
                    {
                        OrderID = order.OrderID,
                        OrderNumber = order.OrderNumber,
                        CustomerName = order.Customer.User.FullName,
                        TotalAmount = order.TotalAmount,
                        Status = order.Status,
                        OrderDate = order.OrderDate,
                        ItemCount = itemCount
                    });
                }

                return Page();
            }

            public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, string newStatus)
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                    return RedirectToPage("/Account/AccessDenied");

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found.";
                    return RedirectToPage();
                }

                // Verify order belongs to this store
                var hasStoreItem = await _context.OrderItems
                    .AnyAsync(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID);

                if (!hasStoreItem)
                {
                    TempData["ErrorMessage"] = "Unauthorized access to this order.";
                    return RedirectToPage();
                }

                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToPage();
                }

                order.Status = newStatus;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to {newStatus}.";
                return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
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
        }
    }