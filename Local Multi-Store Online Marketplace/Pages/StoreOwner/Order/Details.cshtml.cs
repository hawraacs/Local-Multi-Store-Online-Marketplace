using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Local_Multi_Store_Online_Marketplace.Hubs;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    [Authorize(Roles = "StoreOwner")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IHubContext<OrderHub> _hubContext; // added

        public DetailsModel(ApplicationDbContext context, ICurrentStoreService currentStoreService, IHubContext<OrderHub> hubContext) // hubContext added
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _hubContext = hubContext; // added
        }

        public OrderDetailsViewModel? Order { get; set; }
        public List<OrderItemViewModel> OrderItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
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
                CustomerName = order.Customer.User.FullName,
                CustomerEmail = order.Customer.User.Email ?? "",
                // If your Customer entity has Phone property, replace "Not provided" with order.Customer.Phone
                CustomerPhone = "Not provided",
                // If your Order entity has a shipping address property, replace the next line
                ShippingAddress = "No address stored",
                Status = order.Status,
                OrderDate = order.OrderDate,
                Subtotal = subtotal,
                DeliveryFee = order.DeliveryFee,   // Ensure your Order has DeliveryFee
                TotalAmount = order.TotalAmount,
                IsMultiVendor = distinctStoreCount > 1
            };

            OrderItems = items.Select(i => new OrderItemViewModel
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                // If you have UnitPrice or Price, use it; otherwise calculate
                Price = i.TotalPrice / i.Quantity,
                TotalPrice = i.TotalPrice
            }).ToList();

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
                return RedirectToPage(new { id = orderId });
            }

            var hasStoreItem = await _context.OrderItems
                .AnyAsync(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID);

            if (!hasStoreItem)
            {
                TempData["ErrorMessage"] = "Unauthorized access to this order.";
                return RedirectToPage("/StoreOwner/Order/Index");
            }

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage("/StoreOwner/Order/Index");
            }

            // Fix for H4 (partial, Option 1): same guard as Index.cshtml.cs.
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

            // Backend protection: OutForDelivery and Delivered belong
            // only to the Delivery Person workflow. A Store Owner must
            // not be able to set either from here, even via a manual POST.
            if (string.Equals(newStatus, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newStatus, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "'" + newStatus + "' can only be set by the delivery person, not from here.";

                return RedirectToPage(new { id = orderId });
            }

            // Backend protection: once an order is already Out for
            // Delivery or Delivered, a Store Owner can no longer
            // cancel it from here.
            if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(order.Status, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(order.Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] =
                    "This order is already out for delivery or delivered and cannot be cancelled.";

                return RedirectToPage(new { id = orderId });
            }

            order.Status = newStatus;

            // ==========================================
            // DELIVERY NOTIFICATION — "Ready for Pickup"
            //
            // Notifies the delivery person already assigned to this
            // order (via the existing DeliveryAssignment) that the
            // store has finished preparing it. Added only — does not
            // touch the status/guard logic above or the existing
            // SaveChangesAsync call below.
            // ==========================================
            if (string.Equals(newStatus, "Ready for Pickup", StringComparison.OrdinalIgnoreCase))
            {
                var assignedDeliveryPerson = await _context.DeliveryAssignments
                    .Where(a =>
                        a.OrderID == orderId &&
                        a.Status != "Delivered" &&
                        a.Status != "Cancelled" &&
                        a.Status != "Failed")
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => a.DeliveryPerson)
                    .FirstOrDefaultAsync();

                if (assignedDeliveryPerson != null)
                {
                    var notificationAlreadyExists = await _context.Notifications
                        .AnyAsync(n =>
                            n.UserID == assignedDeliveryPerson.UserID &&
                            n.Type == "DeliveryReadyForPickup" &&
                            n.ReferenceID == order.OrderID);

                    if (!notificationAlreadyExists)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserID = assignedDeliveryPerson.UserID,
                            Title = "Order Ready for Pickup",
                            Message = $"Order {order.OrderNumber} is ready for pickup at the store.",
                            Type = "DeliveryReadyForPickup",
                            ReferenceID = order.OrderID,
                            IsRead = false,
                            SentAt = DateTime.UtcNow,
                            SentVia = "System"
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            // added â€” only reached if SaveChangesAsync above completed without throwing
            Console.WriteLine($"[SignalR-DEBUG] About to send OrderStatusUpdated for OrderID={order.OrderID}, newStatus={newStatus}"); // temporary debug log
            await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", order.OrderID, newStatus);
            Console.WriteLine($"[SignalR-DEBUG] SendAsync completed for OrderID={order.OrderID}"); // temporary debug log

            TempData["SuccessMessage"] = $"Order #{order.OrderNumber} status updated to {newStatus}.";
            return RedirectToPage(new { id = orderId });
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