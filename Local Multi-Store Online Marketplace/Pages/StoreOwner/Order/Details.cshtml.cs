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
using Stripe;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    [Authorize(Roles = "StoreOwner")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IHubContext<OrderHub> _hubContext; // added
        private readonly IHubContext<AppHub> _appHubContext; // NEW — CustomerOrders.cshtml only listens on the shared AppHub connection, not OrderHub, so the new customer toast is sent through this instead
        private readonly IConfiguration _configuration; // NEW — for Stripe:SecretKey, same pattern as OnlinePaymentModel
        private readonly ILogger<DetailsModel> _logger; // NEW — logs refund failures without blocking the cancel action

        public DetailsModel(ApplicationDbContext context, ICurrentStoreService currentStoreService, IHubContext<OrderHub> hubContext, IHubContext<AppHub> appHubContext, IConfiguration configuration, ILogger<DetailsModel> logger) // hubContext added
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _hubContext = hubContext; // added
            _appHubContext = appHubContext;
            _configuration = configuration;
            _logger = logger;
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

            // Fix for multi-store confirmation: this store's own response
            // to its part of the order (all of this store's items always
            // share the same StoreResponseStatus).
            var thisStoreStatus = items.FirstOrDefault()?.StoreResponseStatus ?? "Pending";

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
                IsMultiVendor = distinctStoreCount > 1,
                // Single-vendor orders: this store's response IS the
                // order's status, exactly as before.
                StoreStatus = distinctStoreCount > 1 ? thisStoreStatus : order.Status
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

            // ==========================================================
            // MULTI-STORE CONFIRMATION (business-logic fix)
            //
            // Same fix as Index.cshtml.cs's OnPostUpdateStatusAsync: for a
            // multi-vendor order, Confirmed/Cancelled are recorded per
            // store on OrderItem.StoreResponseStatus instead of
            // overwriting the shared Order.Status directly. The shared
            // Order.Status only advances once every participating store
            // has responded (>=1 Confirmed -> "Confirmed", all Cancelled
            // -> "Cancelled"; any store still Pending -> stays "Pending").
            // Single-vendor orders fall straight through to the original
            // logic below, unchanged.
            // ==========================================================
            var distinctStoreCountForOrder = await _context.OrderItems
                .Where(oi => oi.OrderID == orderId)
                .Select(oi => oi.StoreID)
                .Distinct()
                .CountAsync();

            var isMultiVendorConfirmOrCancel =
                distinctStoreCountForOrder > 1 &&
                (string.Equals(newStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase));

            if (isMultiVendorConfirmOrCancel)
            {
                if (string.Equals(order.Status, "Preparing", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "This order has already moved past the confirmation stage.";

                    return RedirectToPage(new { id = orderId });
                }

                var storeItems = await _context.OrderItems
                    .Where(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID)
                    .ToListAsync();

                var currentStoreStatus = storeItems.FirstOrDefault()?.StoreResponseStatus ?? "Pending";

                if (!string.Equals(currentStoreStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "You have already responded to this order (" + currentStoreStatus + ").";

                    return RedirectToPage(new { id = orderId });
                }

                foreach (var item in storeItems)
                    item.StoreResponseStatus = newStatus;

                // ==========================================
                // VERIFIED STATUS WRITE — SAVED AND CONFIRMED
                // BEFORE ANY MONEY MOVES
                // Same fix as Index.cshtml.cs's OnPostUpdateStatusAsync —
                // see that file for the full explanation. Saves the
                // status change in its own isolated step, then re-reads
                // straight from the database (AsNoTracking) to confirm it
                // actually took, before any money/refund code is allowed
                // to run.
                // ==========================================
                await _context.SaveChangesAsync();

                var verifiedStoreStatuses = await _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID)
                    .Select(oi => oi.StoreResponseStatus)
                    .ToListAsync();

                var statusWriteConfirmed =
                    verifiedStoreStatuses.Count > 0 &&
                    verifiedStoreStatuses.All(s => string.Equals(s, newStatus, StringComparison.OrdinalIgnoreCase));

                if (!statusWriteConfirmed)
                {
                    _logger.LogError(
                        "StoreResponseStatus write for OrderID={OrderId} StoreID={StoreId} did not verify after save. " +
                        "Expected '{NewStatus}', found [{Actual}]. No money/refund was touched.",
                        orderId, store.StoreID, newStatus, string.Join(",", verifiedStoreStatuses));

                    TempData["ErrorMessage"] =
                        "Your response could not be saved. Nothing was changed — please try again.";

                    return RedirectToPage(new { id = orderId });
                }

                // ==========================================
                // MONEY ADJUSTMENT FOR A CANCELLED STORE
                // Same logic as Index.cshtml.cs's OnPostUpdateStatusAsync
                // — see that file for the full explanation. Reduces the
                // order by exactly this store's item subtotal, refunds
                // that amount via Stripe if already paid online, and
                // otherwise just lowers what COD collects.
                // ==========================================
                string? customerToastMessage = null;

                if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var cancelledAmount = storeItems.Sum(i => i.TotalPrice);

                    if (cancelledAmount > 0)
                    {
                        order.Subtotal = Math.Max(0, order.Subtotal - cancelledAmount);
                        order.TotalAmount = Math.Max(0, order.TotalAmount - cancelledAmount);

                        var isOnlinePaid =
                            string.Equals(order.PaymentMethod, "Online Payment", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);

                        if (isOnlinePaid)
                        {
                            var latestPayment = await _context.Payments
                                .Where(p => p.OrderID == order.OrderID)
                                .OrderByDescending(p => p.PaymentDate)
                                .FirstOrDefaultAsync();

                            var refunded = false;

                            if (latestPayment != null &&
                                string.Equals(latestPayment.PaymentGateway, "Stripe", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(latestPayment.GatewayTransactionID))
                            {
                                var secretKey = _configuration["Stripe:SecretKey"];

                                if (!string.IsNullOrWhiteSpace(secretKey) &&
                                    !secretKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        StripeConfiguration.ApiKey = secretKey;

                                        var refundService = new RefundService();

                                        await refundService.CreateAsync(new RefundCreateOptions
                                        {
                                            PaymentIntent = latestPayment.GatewayTransactionID,
                                            Amount = (long)Math.Round(cancelledAmount * 100m, 0)
                                        });

                                        latestPayment.RefundAmount = (latestPayment.RefundAmount ?? 0) + cancelledAmount;
                                        latestPayment.RefundDate = DateTime.UtcNow;

                                        if (order.TotalAmount <= 0)
                                            order.PaymentStatus = "Refunded";

                                        refunded = true;
                                    }
                                    catch (StripeException ex)
                                    {
                                        _logger.LogError(ex,
                                            "Stripe refund of {Amount} failed for order {OrderId} (store {StoreId}).",
                                            cancelledAmount, order.OrderID, store.StoreID);
                                    }
                                }
                            }

                            customerToastMessage = refunded
                                ? $"${cancelledAmount:N2} has been refunded because the store cancelled your items."
                                : $"A store cancelled ${cancelledAmount:N2} of your order. Your refund is being processed — contact support if you don't see it soon.";
                        }
                        else
                        {
                            customerToastMessage =
                                $"${cancelledAmount:N2} was removed from your order because the store cancelled those items.";
                        }
                    }
                }

                var allItemStatuses = await _context.OrderItems
                    .Where(oi => oi.OrderID == orderId)
                    .Select(oi => new { oi.StoreID, oi.StoreResponseStatus })
                    .ToListAsync();

                var allStoreStatuses = allItemStatuses
                    .GroupBy(x => x.StoreID)
                    .Select(g => g.First().StoreResponseStatus)
                    .ToList();

                var overallStatus = AllowedOrderStatuses.ComputeOverallStatus(allStoreStatuses);

                if (overallStatus != null)
                    order.Status = overallStatus;
                // else: another store hasn't responded yet — Order.Status
                // stays "Pending", exactly as it started.

                await _context.SaveChangesAsync();

                try
                {
                    await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", order.OrderID, order.Status);

                    // NEW — targeted toast to just this customer, sent via
                    // AppHub since that's what CustomerOrders.cshtml
                    // actually listens on (this page's own _hubContext is
                    // the separate OrderHub). Reuses the existing
                    // showDeliveryReviewToast() toast UI — no new toast
                    // system. Only sent when this Cancel action actually
                    // changed money owed.
                    if (customerToastMessage != null)
                    {
                        var customerUserId = await _context.Orders
                            .Where(o => o.OrderID == orderId)
                            .Select(o => o.Customer.UserID)
                            .FirstOrDefaultAsync();

                        if (customerUserId > 0)
                        {
                            await _appHubContext.Clients.User(customerUserId.ToString())
                                .SendAsync("OrderPartialUpdate", customerToastMessage);
                        }
                    }
                }
                catch
                {
                    // Never let a broadcast failure break the status update itself.
                }

                TempData["SuccessMessage"] = overallStatus != null
                    ? $"Order #{order.OrderNumber} status updated to {order.Status}." + (customerToastMessage != null ? " " + customerToastMessage : "")
                    : "Your response has been recorded. Waiting for the other store(s) to respond." + (customerToastMessage != null ? " " + customerToastMessage : "");

                return RedirectToPage(new { id = orderId });
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
            // DELIVERY NOTIFICATION � "Ready for Pickup"
            //
            // Notifies the delivery person already assigned to this
            // order (via the existing DeliveryAssignment) that the
            // store has finished preparing it. Added only � does not
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

            // added — only reached if SaveChangesAsync above completed without throwing
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

        // Fix for multi-store confirmation: this store's own response to
        // its part of the order ("Pending"/"Confirmed"/"Cancelled"). For
        // single-vendor orders this always mirrors Status.
        public string StoreStatus { get; set; } = string.Empty;
    }

    public class OrderItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice { get; set; }
    }
}