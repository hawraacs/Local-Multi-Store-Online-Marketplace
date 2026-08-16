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
using Microsoft.AspNetCore.SignalR;
using Local_Multi_Store_Online_Marketplace.Hubs;
using Stripe;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IHubContext<AppHub> _hubContext; // CHANGED — was IHubContext<OrderHub>
        private readonly IConfiguration _configuration; // NEW — for Stripe:SecretKey, same pattern as OnlinePaymentModel
        private readonly ILogger<IndexModel> _logger; // NEW — logs refund failures without blocking the cancel action

        public IndexModel(ApplicationDbContext context, ICurrentStoreService currentStoreService, IHubContext<AppHub> hubContext, IConfiguration configuration, ILogger<IndexModel> logger) // CHANGED
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _hubContext = hubContext;
            _configuration = configuration;
            _logger = logger;
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
                // Fix for multi-store confirmation: also pull this store's
                // own StoreResponseStatus (all of this store's items for
                // this order always share the same value) so the view can
                // gate the Confirm/Cancel action per-store, not per-order.
                var thisStoreItemStatuses = await _context.OrderItems
                    .Where(oi => oi.OrderID == order.OrderID && oi.StoreID == store.StoreID)
                    .Select(oi => oi.StoreResponseStatus)
                    .ToListAsync();

                var itemCount = thisStoreItemStatuses.Count;
                var thisStoreStatus = thisStoreItemStatuses.FirstOrDefault() ?? "Pending";

                // Fix for H4 (partial, Option 1): does this order contain
                // items from more than one store? If so, Order.Status is
                // shared with other stores and this view must not let this
                // store owner end the whole order (Delivered/Cancelled).
                var distinctStoreCount = await _context.OrderItems
                    .Where(oi => oi.OrderID == order.OrderID)
                    .Select(oi => oi.StoreID)
                    .Distinct()
                    .CountAsync();

                // Fix: Online Payment orders (single-store) reach "Preparing"
                // directly after payment instead of "Confirmed" — see
                // IsAwaitingFirstOnlinePaymentConfirmationAsync above. Only
                // relevant for single-vendor orders, since Online Payment
                // never sets the shared Order.Status to "Preparing" for a
                // multi-vendor order (that path uses StoreResponseStatus).
                var awaitingOnlinePaymentConfirmation = distinctStoreCount <= 1 &&
                    await IsAwaitingFirstOnlinePaymentConfirmationAsync(
                        order.OrderID, order.PaymentMethod, order.Status);

                Orders.Add(new OrderViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    CustomerName = order.Customer.User.FullName,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    OrderDate = order.OrderDate,
                    ItemCount = itemCount,
                    IsMultiVendor = distinctStoreCount > 1,
                    // Single-vendor orders: this store's response IS the
                    // order's status, exactly as before. Multi-vendor
                    // orders: this store's own per-store response.
                    StoreStatus = distinctStoreCount > 1 ? thisStoreStatus : order.Status,
                    AwaitingOnlinePaymentConfirmation = awaitingOnlinePaymentConfirmation
                });
            }

            return Page();
        }

        // =====================================================
        // Fix: Order.Status "Preparing" is now reached two different
        // ways: (a) a single-store Online Payment order lands here
        // straight from "Pending"/"Pending Confirmation" right after a
        // successful Stripe charge (see OnlinePayment.cshtml.cs),
        // awaiting the Store Owner's first Confirm/Cancel, and (b) the
        // pre-existing case where a Store Owner already Confirmed the
        // order and later moved it to "Preparing" themselves while
        // getting it ready for delivery. Both look identical as just
        // Status == "Preparing", so this looks at the order's own
        // OrderStatusHistories (already written, no schema change) to
        // tell them apart: only case (a) is eligible for Confirm/Cancel.
        // =====================================================
        private async Task<bool> IsAwaitingFirstOnlinePaymentConfirmationAsync(
            int orderId, string? paymentMethod, string? currentStatus)
        {
            if (!string.Equals(currentStatus, "Preparing", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(paymentMethod, "Online Payment", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var latestPreparingHistory = await _context.OrderStatusHistories
                .Where(h => h.OrderID == orderId && h.NewStatus == "Preparing")
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();

            return latestPreparingHistory != null &&
                (string.Equals(latestPreparingHistory.PreviousStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(latestPreparingHistory.PreviousStatus, "Pending Confirmation", StringComparison.OrdinalIgnoreCase));
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

            // Switched from FindAsync to a query with Include so we have
            // order.Customer.UserID available for the notification below.
            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage();
            }

            // ==========================================================
            // MULTI-STORE CONFIRMATION (business-logic fix)
            //
            // Order.Status is a single field shared by every store in a
            // multi-vendor order. Previously, whichever store acted first
            // on "Confirmed"/"Cancelled" overwrote that shared field for
            // every other store too, so a second store could never
            // respond independently, and the H4 guard below simply
            // blocked "Cancelled" outright for multi-vendor orders.
            //
            // For a multi-vendor order, Confirmed/Cancelled are now
            // recorded per store on OrderItem.StoreResponseStatus, and
            // the shared Order.Status is only advanced once every
            // participating store has responded:
            //   - all responded, >=1 Confirmed -> Order.Status "Confirmed"
            //   - all responded, all Cancelled  -> Order.Status "Cancelled"
            //   - any store still Pending       -> Order.Status stays "Pending"
            //
            // Single-vendor orders are NOT touched by this block and fall
            // straight through to the original logic below, unchanged.
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
                // Once the order has moved past the confirmation stage
                // (admin already assigned delivery, or beyond), a store
                // can no longer confirm/cancel its part from here.
                if (string.Equals(order.Status, "Preparing", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "This order has already moved past the confirmation stage.";

                    return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
                }

                var storeItems = await _context.OrderItems
                    .Where(oi => oi.OrderID == orderId && oi.StoreID == store.StoreID)
                    .ToListAsync();

                var currentStoreStatus = storeItems.FirstOrDefault()?.StoreResponseStatus ?? "Pending";

                if (!string.Equals(currentStoreStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "You have already responded to this order (" + currentStoreStatus + ").";

                    return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
                }

                foreach (var item in storeItems)
                    item.StoreResponseStatus = newStatus;

                // ==========================================
                // VERIFIED STATUS WRITE — SAVED AND CONFIRMED
                // BEFORE ANY MONEY MOVES
                //
                // Fix for a confirmed bug: the in-memory assignment above
                // was not reliably ending up persisted for OrderItem even
                // though Order money fields saved fine in the same
                // request. Rather than assume the write succeeded, this
                // now saves it in its own isolated step, then re-reads
                // straight from the database (AsNoTracking, bypassing any
                // tracked/cached state) to PROVE it actually took. Money
                // adjustment / Stripe refund code below only ever runs if
                // that verification passes — this is the "status success
                // -> money; status failure -> no money" guarantee.
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

                    return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
                }

                // ==========================================
                // MONEY ADJUSTMENT FOR A CANCELLED STORE
                //
                // Only runs for "Cancelled". Reduces the order by exactly
                // this store's item subtotal — DeliveryFee/DiscountAmount/
                // TaxAmount are left untouched (no reliable per-store
                // breakdown exists for them). For Online Payment that was
                // already Paid, refunds that same amount via Stripe using
                // the existing PaymentIntent (GatewayTransactionID). For
                // Cash On Delivery, just lowers what the delivery person
                // collects, since Order.TotalAmount is what COD collection
                // already reads elsewhere.
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
                            // Cash On Delivery (or any non-online method):
                            // nothing was charged yet, so simply lowering
                            // Order.TotalAmount above is enough to reduce
                            // what the delivery person collects.
                            customerToastMessage =
                                $"${cancelledAmount:N2} was removed from your order because the store cancelled those items.";
                        }
                    }
                }

                // Recompute the shared Order.Status from every
                // participating store's current response (one
                // representative status per store).
                var allItemStatuses = await _context.OrderItems
                    .Where(oi => oi.OrderID == orderId)
                    .Select(oi => new { oi.StoreID, oi.StoreResponseStatus })
                    .ToListAsync();

                var allStoreStatuses = allItemStatuses
                    .GroupBy(x => x.StoreID)
                    .Select(g => g.First().StoreResponseStatus)
                    .ToList();

                var previousOverallStatus = order.Status;
                var overallStatus = AllowedOrderStatuses.ComputeOverallStatus(allStoreStatuses);

                if (overallStatus != null)
                    order.Status = overallStatus;
                // else: another store hasn't responded yet — Order.Status
                // stays "Pending", exactly as it started.

                if (order.Customer != null)
                {
                    var storeMessage = string.Equals(newStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)
                        ? $"{store.StoreName} confirmed its items in your order {order.OrderNumber}."
                        : $"{store.StoreName} cancelled its items in your order {order.OrderNumber}.";

                    _context.Notifications.Add(new Notification
                    {
                        UserID = order.Customer.UserID,
                        Title = "Store responded to your order",
                        Message = storeMessage,
                        Type = "OrderStatus",
                        ReferenceID = order.OrderID,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        SentVia = "System"
                    });

                    if (overallStatus != null &&
                        !string.Equals(previousOverallStatus, overallStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        var overallMessage = string.Equals(overallStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)
                            ? $"Your order {order.OrderNumber} has been confirmed and will move to delivery."
                            : $"Your order {order.OrderNumber} has been cancelled.";

                        _context.Notifications.Add(new Notification
                        {
                            UserID = order.Customer.UserID,
                            Title = "Order status updated",
                            Message = overallMessage,
                            Type = "OrderStatus",
                            ReferenceID = order.OrderID,
                            IsRead = false,
                            SentAt = DateTime.UtcNow,
                            SentVia = "System"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                try
                {
                    await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", order.OrderID, order.Status);

                    // NEW — targeted toast to just this customer (reuses
                    // the existing showDeliveryReviewToast() toast UI
                    // already in CustomerOrders.cshtml, no new toast
                    // system). Only sent when this Cancel action actually
                    // changed money owed.
                    if (customerToastMessage != null && order.Customer != null)
                    {
                        await _hubContext.Clients.User(order.Customer.UserID.ToString())
                            .SendAsync("OrderPartialUpdate", customerToastMessage);
                    }
                }
                catch
                {
                    // Never let a broadcast failure break the status update itself.
                }

                TempData["SuccessMessage"] = overallStatus != null
                    ? $"Order #{order.OrderNumber} status updated to {order.Status}." + (customerToastMessage != null ? " " + customerToastMessage : "")
                    : "Your response has been recorded. Waiting for the other store(s) to respond." + (customerToastMessage != null ? " " + customerToastMessage : "");

                return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
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

                    return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
                }
            }

            // Backend protection: OutForDelivery and Delivered belong only
            // to the Delivery Person workflow (started/completed via
            // DeliveryOrderDetails / DeliveryDashboard). A Store Owner must
            // not be able to set either from here, even via a manual POST.
            if (string.Equals(newStatus, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newStatus, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "'" + newStatus + "' can only be set by the delivery person, not from here.";

                return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
            }

            // Backend protection: once an order is already Out for Delivery
            // or Delivered, a Store Owner can no longer cancel it from here.
            if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(order.Status, "Out for Delivery", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(order.Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] =
                    "This order is already out for delivery or delivered and cannot be cancelled.";

                return RedirectToPage(new { pageIndex = PageIndex, statusFilter = StatusFilter, searchTerm = SearchTerm });
            }

            var previousStatus = order.Status;

            if (order.Status == "Pending" &&
                newStatus != "Confirmed" &&
                newStatus != "Cancelled")
            {
                TempData["ErrorMessage"] = "Invalid status transition.";
                return RedirectToPage();
            }

            if (order.Status == "Confirmed" &&
                newStatus != "Preparing" &&
                newStatus != "Cancelled")
            {
                TempData["ErrorMessage"] = "Invalid status transition.";
                return RedirectToPage();
            }

            if (order.Status == "Preparing")
            {
                // Fix: an Online Payment order sitting in "Preparing" that
                // has never actually been Confirmed yet (see
                // IsAwaitingFirstOnlinePaymentConfirmationAsync above) must
                // still let the Store Owner Confirm or Cancel it here. Any
                // other "Preparing" order (already Confirmed earlier, now
                // being prepared for delivery) keeps the original behavior
                // exactly as before.
                var isAwaitingFirstConfirmation =
                    await IsAwaitingFirstOnlinePaymentConfirmationAsync(
                        orderId, order.PaymentMethod, order.Status);

                var isConfirmOrCancel =
                    newStatus == "Confirmed" || newStatus == "Cancelled";

                if (!isAwaitingFirstConfirmation || !isConfirmOrCancel)
                {
                    TempData["ErrorMessage"] =
                        "This order is waiting for admin delivery assignment.";

                    return RedirectToPage();
                }
            }


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

            // CHANGED — now broadcasts via the shared AppHub, same
            // connection used by Explore's live product feed and chat,
            // instead of a separate dedicated OrderHub connection.
            try
            {
                await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", order.OrderID, newStatus);
            }
            catch
            {
                // Never let a broadcast failure break the status update
                // itself — the order is already safely saved above.
            }

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
        public bool IsMultiVendor { get; set; }

        // Fix for multi-store confirmation: this store's own response to
        // its part of the order ("Pending"/"Confirmed"/"Cancelled"). For
        // single-vendor orders this always mirrors Status.
        public string StoreStatus { get; set; } = string.Empty;

        // Fix: true only for a single-vendor Online Payment order that is
        // "Preparing" but has never actually been Confirmed by this Store
        // Owner yet — lets the view show Confirm/Cancel instead of plain
        // status text for that specific case.
        public bool AwaitingOnlinePaymentConfirmation { get; set; }
    }
}