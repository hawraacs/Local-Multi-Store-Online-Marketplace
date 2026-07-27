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
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            ILogger<DetailsModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _logger = logger;
        }

        public OrderDetailsViewModel? Order { get; set; }
        public List<OrderItemViewModel> OrderItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                    return RedirectToPage("/Account/AccessDenied");

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found.";
                    return RedirectToPage("/StoreOwner/Order/Index");
                }

                // Check if this order belongs to the store
                var hasStoreItem = await _context.OrderItems
                    .AnyAsync(oi => oi.OrderID == id && oi.StoreID == store.StoreID);

                if (!hasStoreItem)
                {
                    TempData["ErrorMessage"] = "Unauthorized access to this order.";
                    return RedirectToPage("/StoreOwner/Order/Index");
                }

                // Load order with customer
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                    .FirstOrDefaultAsync(o => o.OrderID == id);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToPage("/StoreOwner/Order/Index");
                }

                // Load only the order items belonging to this store
                var items = await _context.OrderItems
                    .Where(oi => oi.OrderID == id && oi.StoreID == store.StoreID)
                    .ToListAsync();

                // Compute subtotal
                var subtotal = items.Sum(i => i.TotalPrice);

                // Fix for H4 (partial, Option 1): does this order contain
                // items from more than one store?
                var distinctStoreCount = await _context.OrderItems
                    .Where(oi => oi.OrderID == id)
                    .Select(oi => oi.StoreID)
                    .Distinct()
                    .CountAsync();

                // Prepare view models
                Order = new OrderDetailsViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    // BUGFIX: no null-safety before — order.Customer.User.FullName would
                    // throw if either navigation property were ever null. The sibling
                    // Orders list page already guards this the same way.
                    CustomerName = order.Customer?.User?.FullName ?? "Customer",
                    CustomerEmail = order.Customer?.User?.Email ?? "",
                    // TODO: wire up to a real field once confirmed — see chat notes.
                    // If your Customer entity has a Phone property, replace this line
                    // with order.Customer?.Phone ?? "Not provided".
                    CustomerPhone = "Not provided",
                    // TODO: wire up to a real field once confirmed — see chat notes.
                    // If your Order entity has a shipping address property, replace
                    // this line with order.ShippingAddress ?? "No address provided".
                    ShippingAddress = "No address stored",
                    Status = order.Status,
                    OrderDate = order.OrderDate,
                    Subtotal = subtotal,
                    DeliveryFee = order.DeliveryFee,
                    TotalAmount = order.TotalAmount,
                    IsMultiVendor = distinctStoreCount > 1
                };

                OrderItems = items.Select(i => new OrderItemViewModel
                {
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    // BUGFIX: divides by i.Quantity with no guard — an item with
                    // Quantity == 0 (shouldn't normally happen, but nothing enforced
                    // it) would throw DivideByZeroException and crash the whole page.
                    Price = i.Quantity > 0 ? i.TotalPrice / i.Quantity : 0,
                    TotalPrice = i.TotalPrice
                }).ToList();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Order Details for order {OrderId}.", id);
                TempData["ErrorMessage"] = "Something went wrong while loading this order. Please try again.";
                return RedirectToPage("/StoreOwner/Order/Index");
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, string newStatus)
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found.";
                return RedirectToPage(new { id = orderId });
            }

            // BUGFIX: newStatus was previously written straight to the database with
            // no validation — same issue as the Orders list page, fixed the same way
            // (shared allow-list on IndexModel so both pages can never drift apart).
            if (string.IsNullOrWhiteSpace(newStatus) ||
                !IndexModel.ValidOrderStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "That isn't a valid order status.";
                return RedirectToPage(new { id = orderId });
            }

            try
            {
                var hasStoreItem = await _context.OrderItems
                    .AnyAsync(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID);

                if (!hasStoreItem)
                {
                    TempData["ErrorMessage"] = "Unauthorized access to this order.";
                    return RedirectToPage("/StoreOwner/Order/Index");
                }

                // BUGFIX: previously used _context.Orders.FindAsync(orderId), which
                // doesn't let you Include() related data — meaning there was no way
                // to notify the customer here. Switched to a query with Include, the
                // same pattern already used in OnGetAsync and in Orders/Index.cs.
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.OrderID == orderId);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToPage("/StoreOwner/Order/Index");
                }

                // Fix for H4 (partial, Option 1): same guard as Index.cshtml.cs —
                // a multi-vendor order can't be marked Delivered/Cancelled from
                // this single-store page since other stores' items are unaffected.
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

                        return RedirectToPage(new { id = orderId });
                    }
                }

                var previousStatus = order.Status;
                order.Status = newStatus;

                // BUGFIX: this page previously updated status with NO customer
                // notification at all — the sibling Orders/Index page notifies the
                // customer on status change, so updating from here instead silently
                // skipped it. Same notification logic as that page, for consistency
                // regardless of which page the store owner happens to use.
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
                return RedirectToPage(new { id = orderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for Order {OrderId}.", orderId);
                TempData["ErrorMessage"] = "Something went wrong while updating the order. Please try again.";
                return RedirectToPage(new { id = orderId });
            }
        }
    }

    public class OrderDetailsViewModel
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsMultiVendor { get; set; }
    }

    public class OrderItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice { get; set; }
    }
}