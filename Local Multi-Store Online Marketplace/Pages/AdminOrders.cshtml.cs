using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.Security.Claims;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminOrdersModel> _logger;

        public AdminOrdersModel(
            ApplicationDbContext context,
            ILogger<AdminOrdersModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<AdminOrderDto> Orders { get; set; } = new();

        public int TotalOrders { get; set; }

        public decimal TotalRevenue { get; set; }

        public int PendingCount { get; set; }

        public int DeliveredCount { get; set; }

        // =====================================================
        // LOAD ORDERS
        // =====================================================
        public async Task OnGetAsync()
        {
            await LoadOrdersAsync();
        }

        // =====================================================
        // UPDATE ORDER STATUS
        // =====================================================
        public async Task<IActionResult> OnPostUpdateStatusAsync(
            int orderId,
            string? newStatus)
        {
            if (orderId <= 0)
            {
                TempData["Error"] =
                    "Invalid order ID.";

                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(newStatus))
            {
                TempData["Error"] =
                    "Please select a valid order status.";

                return RedirectToPage();
            }

            var normalizedNewStatus =
                NormalizeStatus(newStatus);

            try
            {
                var order =
                    await _context.Orders
                        .FirstOrDefaultAsync(o =>
                            o.OrderID == orderId);

                if (order == null)
                {
                    TempData["Error"] =
                        "Order was not found.";

                    return RedirectToPage();
                }

                var currentStatus =
                    NormalizeStatus(order.Status);

                if (string.Equals(
                        currentStatus,
                        normalizedNewStatus,
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Info"] =
                        $"Order {order.OrderNumber} already has this status.";

                    return RedirectToPage();
                }

                if (!IsAllowedAdminTransition(
                        currentStatus,
                        normalizedNewStatus))
                {
                    TempData["Error"] =
                        GetTransitionErrorMessage(
                            currentStatus);

                    return RedirectToPage();
                }

                var previousStatus =
                    order.Status;

                order.Status =
                    normalizedNewStatus;

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
                    {
                        OrderID =
                            order.OrderID,

                        PreviousStatus =
                            previousStatus,

                        NewStatus =
                            normalizedNewStatus,

                        ChangedBy =
                            GetCurrentAdminName(),

                        ChangedAt =
                            DateTime.UtcNow,

                        Notes =
                            "Order status updated from the Admin Orders page."
                    });

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"Order {order.OrderNumber} status was updated to {normalizedNewStatus}.";
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error updating order {OrderId} status.",
                    orderId);

                TempData["Error"] =
                    "The order status could not be updated. Please try again.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // CANCEL ORDER
        // =====================================================
        public async Task<IActionResult> OnPostCancelAsync(
            int orderId,
            string? cancellationReason)
        {
            if (orderId <= 0)
            {
                TempData["Error"] =
                    "Invalid order ID.";

                return RedirectToPage();
            }

            var cleanReason =
                cancellationReason?.Trim();

            if (string.IsNullOrWhiteSpace(cleanReason))
            {
                TempData["Error"] =
                    "A cancellation reason is required.";

                return RedirectToPage();
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                var order =
                    await _context.Orders
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .Include(o => o.Payments)
                        .Include(o => o.DeliveryAssignment)
                            .ThenInclude(assignment =>
                                assignment!.DeliveryPerson)
                        .FirstOrDefaultAsync(o =>
                            o.OrderID == orderId);

                if (order == null)
                {
                    TempData["Error"] =
                        "Order was not found.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                var currentStatus =
                    NormalizeStatus(order.Status);

                if (currentStatus == "Cancelled")
                {
                    TempData["Info"] =
                        $"Order {order.OrderNumber} is already cancelled.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                if (currentStatus == "Out for Delivery" ||
                    currentStatus == "Delivered")
                {
                    TempData["Error"] =
                        "An order that is out for delivery or delivered cannot be cancelled here.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                var hasPaidPayment =
                    string.Equals(
                        order.PaymentStatus,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    order.Payments.Any(payment =>
                        string.Equals(
                            payment.Status,
                            "Paid",
                            StringComparison.OrdinalIgnoreCase));

                if (hasPaidPayment)
                {
                    TempData["Error"] =
                        "This order has already been paid. Process a refund before cancelling it.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                var previousStatus =
                    order.Status;

                // Restore product stock once before cancellation.
                foreach (var orderItem in order.OrderItems)
                {
                    if (orderItem.Product == null)
                    {
                        continue;
                    }

                    orderItem.Product.Quantity +=
                        orderItem.Quantity;

                    orderItem.Product.UpdatedAt =
                        DateTime.UtcNow;
                }

                // Cancel any active delivery assignment.
                if (order.DeliveryAssignment != null &&
                    !string.Equals(
                        order.DeliveryAssignment.Status,
                        "Delivered",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        order.DeliveryAssignment.Status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    order.DeliveryAssignment.Status =
                        "Cancelled";

                    if (order.DeliveryAssignment.DeliveryPerson != null &&
                        string.Equals(
                            order.DeliveryAssignment.DeliveryPerson.Status,
                            "Busy",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        order.DeliveryAssignment
                            .DeliveryPerson
                            .Status =
                                "Available";
                    }
                }

                order.Status =
                    "Cancelled";

                order.CancellationReason =
                    cleanReason;

                order.CancelledAt =
                    DateTime.UtcNow;

                _context.OrderStatusHistories.Add(
                    new OrderStatusHistory
                    {
                        OrderID =
                            order.OrderID,

                        PreviousStatus =
                            previousStatus,

                        NewStatus =
                            "Cancelled",

                        ChangedBy =
                            GetCurrentAdminName(),

                        ChangedAt =
                            DateTime.UtcNow,

                        Notes =
                            cleanReason
                    });

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Order {order.OrderNumber} was cancelled successfully.";
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    exception,
                    "Error cancelling order {OrderId}.",
                    orderId);

                TempData["Error"] =
                    "The order could not be cancelled. No changes were saved.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // LOAD REAL DATA FROM DATABASE
        // =====================================================
        private async Task LoadOrdersAsync()
        {
            var orderEntities =
                await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Customer)
                        .ThenInclude(customer =>
                            customer.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(orderItem =>
                            orderItem.Store)
                    .Include(o => o.DeliveryAssignment)
                        .ThenInclude(assignment =>
                            assignment!.DeliveryPerson)
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            Orders =
                orderEntities
                    .Select(order =>
                    {
                        var customerName =
                            order.Customer?.User?.FullName;

                        if (string.IsNullOrWhiteSpace(
                                customerName))
                        {
                            customerName =
                                order.Customer?.User?.Email;
                        }

                        if (string.IsNullOrWhiteSpace(
                                customerName))
                        {
                            customerName =
                                $"Customer #{order.CustomerID}";
                        }

                        var storeNames =
                            order.OrderItems
                                .Where(item =>
                                    item.Store != null)
                                .Select(item =>
                                    item.Store!.StoreName)
                                .Where(name =>
                                    !string.IsNullOrWhiteSpace(name))
                                .Distinct()
                                .ToList();

                        var storeName =
                            storeNames.Any()
                                ? string.Join(
                                    ", ",
                                    storeNames)
                                : "N/A";

                        var items =
                            order.OrderItems.Any()
                                ? string.Join(
                                    ", ",
                                    order.OrderItems.Select(item =>
                                        $"{item.Quantity}x {item.ProductName}"))
                                : "No items";

                        return new AdminOrderDto
                        {
                            OrderId =
                                order.OrderID,

                            OrderNumber =
                                order.OrderNumber,

                            CustomerName =
                                customerName,

                            StoreName =
                                storeName,

                            TotalAmount =
                                order.TotalAmount,

                            Status =
                                string.IsNullOrWhiteSpace(order.Status)
                                    ? "Pending"
                                    : order.Status,

                            PaymentMethod =
                                order.PaymentMethod,

                            PaymentStatus =
                                order.PaymentStatus,

                            OrderDate =
                                order.OrderDate,

                            Items =
                                items,

                            DeliveryPersonName =
                                order.DeliveryAssignment?
                                    .DeliveryPerson?
                                    .FullName,

                            DeliveryAssignmentStatus =
                                order.DeliveryAssignment?
                                    .Status,

                            CancellationReason =
                                order.CancellationReason
                        };
                    })
                    .ToList();

            TotalOrders =
                Orders.Count;

            // Revenue means money actually marked as paid.
            TotalRevenue =
                Orders
                    .Where(order =>
                        string.Equals(
                            order.PaymentStatus,
                            "Paid",
                            StringComparison.OrdinalIgnoreCase))
                    .Sum(order =>
                        order.TotalAmount);

            PendingCount =
                Orders.Count(order =>
                    string.Equals(
                        NormalizeStatus(order.Status),
                        "Pending",
                        StringComparison.OrdinalIgnoreCase));

            DeliveredCount =
                Orders.Count(order =>
                    string.Equals(
                        NormalizeStatus(order.Status),
                        "Delivered",
                        StringComparison.OrdinalIgnoreCase));
        }

        // =====================================================
        // STATUS FLOW VALIDATION
        // =====================================================
        private static bool IsAllowedAdminTransition(
            string currentStatus,
            string newStatus)
        {
            return currentStatus switch
            {
                "Pending" =>
                    newStatus == "Confirmed",

                "Pending Confirmation" =>
                    newStatus == "Confirmed",

                "Confirmed" =>
                    newStatus == "Preparing",

                "Preparing" =>
                    newStatus == "Ready for Pickup",

                _ =>
                    false
            };
        }

        private static string GetTransitionErrorMessage(
            string currentStatus)
        {
            return currentStatus switch
            {
                "Ready for Pickup" =>
                    "This order is ready for delivery assignment. Use Assign Delivery instead.",

                "Assigned" =>
                    "This order is already assigned. The driver must start the delivery.",

                "Out for Delivery" =>
                    "The driver controls this order while it is out for delivery.",

                "Delivered" =>
                    "A delivered order cannot be changed.",

                "Cancelled" =>
                    "A cancelled order cannot be changed.",

                _ =>
                    $"The current status '{currentStatus}' cannot be changed from this page."
            };
        }

        private static string NormalizeStatus(
            string? status)
        {
            var value =
                status?.Trim()
                    .ToLowerInvariant()
                ?? "pending";

            return value switch
            {
                "pending" =>
                    "Pending",

                "pending confirmation" =>
                    "Pending Confirmation",

                "confirmed" =>
                    "Confirmed",

                "preparing" =>
                    "Preparing",

                "ready for pickup" =>
                    "Ready for Pickup",

                "assigned" =>
                    "Assigned",

                "outfordelivery" =>
                    "Out for Delivery",

                "out for delivery" =>
                    "Out for Delivery",

                "delivered" =>
                    "Delivered",

                "cancelled" =>
                    "Cancelled",

                _ =>
                    status?.Trim() ?? "Pending"
            };
        }

        private string GetCurrentAdminName()
        {
            return User.Identity?.Name
                ?? User.FindFirstValue(
                    ClaimTypes.Email)
                ?? "Admin";
        }
    }

    public class AdminOrderDto
    {
        public int OrderId { get; set; }

        public string OrderNumber { get; set; }
            = string.Empty;

        public string CustomerName { get; set; }
            = string.Empty;

        public string StoreName { get; set; }
            = string.Empty;

        public decimal TotalAmount { get; set; }

        public string Status { get; set; }
            = string.Empty;

        public string PaymentMethod { get; set; }
            = string.Empty;

        public string PaymentStatus { get; set; }
            = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Items { get; set; }
            = string.Empty;

        public string? DeliveryPersonName { get; set; }

        public string? DeliveryAssignmentStatus { get; set; }

        public string? CancellationReason { get; set; }
    }
}

