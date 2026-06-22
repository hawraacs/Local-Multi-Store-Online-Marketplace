using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public DeliveryDashboardModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<DeliveryAssignmentViewModel> Assignments { get; set; }
            = new();

        public List<DeliveryNotificationViewModel> Notifications { get; set; }
            = new();

        public string? ErrorMessage { get; set; }

        // =====================================================
        // LOAD DELIVERY DASHBOARD
        // =====================================================
        public async Task<IActionResult> OnGetAsync()
        {
            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved delivery profile was not found.";

                Assignments =
                    new List<DeliveryAssignmentViewModel>();

                Notifications =
                    new List<DeliveryNotificationViewModel>();

                return Page();
            }

            Assignments =
                await _context.DeliveryAssignments
                    .AsNoTracking()
                    .Include(assignment =>
                        assignment.Order)
                    .ThenInclude(order =>
                        order!.Address)
                    .Where(assignment =>
                        assignment.DeliveryPersonID ==
                            deliveryPerson.DeliveryPersonID &&
                        assignment.Status != "Delivered" &&
                        assignment.Status != "Cancelled" &&
                        assignment.Status != "Failed")
                    .OrderByDescending(assignment =>
                        assignment.AssignedAt)
                    .Select(assignment =>
                        new DeliveryAssignmentViewModel
                        {
                            AssignmentID =
                                assignment.AssignmentID,

                            OrderID =
                                assignment.OrderID,

                            OrderNumber =
                                assignment.Order != null
                                    ? assignment.Order.OrderNumber
                                    : string.Empty,

                            OrderStatus =
                                assignment.Order != null
                                    ? assignment.Order.Status
                                    : string.Empty,

                            AssignmentStatus =
                                assignment.Status,

                            CustomerAddress =
                                assignment.Order != null &&
                                assignment.Order.Address != null
                                    ? $"{assignment.Order.Address.AddressLine1}, " +
                                      $"{assignment.Order.Address.Area}, " +
                                      $"{assignment.Order.Address.City}"
                                    : "No address",

                            AssignedAt =
                                assignment.AssignedAt
                        })
                    .ToListAsync();

            var assignedOrderIds =
                Assignments
                    .Where(assignment =>
                        assignment.AssignmentStatus == "Assigned" &&
                        assignment.OrderStatus == "Assigned")
                    .Select(assignment =>
                        assignment.OrderID)
                    .ToList();

            Notifications =
                await _context.Notifications
                    .AsNoTracking()
                    .Where(notification =>
                        notification.UserID ==
                            deliveryPerson.UserID &&
                        notification.Type ==
                            "DeliveryAssignment" &&
                        notification.ReferenceID.HasValue &&
                        assignedOrderIds.Contains(
                            notification.ReferenceID.Value) &&
                        !notification.IsRead)
                    .OrderByDescending(notification =>
                        notification.SentAt)
                    .Select(notification =>
                        new DeliveryNotificationViewModel
                        {
                            NotificationID =
                                notification.NotificationID,

                            Title =
                                notification.Title,

                            Message =
                                notification.Message,

                            SentAt =
                                notification.SentAt,

                            IsRead =
                                notification.IsRead,

                            ReferenceID =
                                notification.ReferenceID
                        })
                    .ToListAsync();

            return Page();
        }

        // =====================================================
        // START DELIVERY
        // =====================================================
        public async Task<IActionResult>
            OnPostStartDeliveryAsync(
                int assignmentId)
        {
            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                TempData["Error"] =
                    "Approved delivery profile was not found.";

                return RedirectToPage();
            }

            var assignment =
                await _context.DeliveryAssignments
                    .Include(deliveryAssignment =>
                        deliveryAssignment.Order)
                    .FirstOrDefaultAsync(
                        deliveryAssignment =>
                            deliveryAssignment.AssignmentID ==
                                assignmentId &&
                            deliveryAssignment.DeliveryPersonID ==
                                deliveryPerson.DeliveryPersonID);

            if (assignment == null ||
                assignment.Order == null)
            {
                TempData["Error"] =
                    "Assignment not found.";

                return RedirectToPage();
            }

            if (string.Equals(
                    assignment.Status,
                    "Delivered",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "This order is already delivered.";

                return RedirectToPage();
            }

            if (!string.Equals(
                    assignment.Status,
                    "Assigned",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "Only assigned orders can be started.";

                return RedirectToPage();
            }

            var alreadyHasRunningDelivery =
                await _context.DeliveryAssignments
                    .Include(deliveryAssignment =>
                        deliveryAssignment.Order)
                    .AnyAsync(deliveryAssignment =>
                        deliveryAssignment.DeliveryPersonID ==
                            deliveryPerson.DeliveryPersonID &&
                        deliveryAssignment.AssignmentID !=
                            assignment.AssignmentID &&
                        deliveryAssignment.Status ==
                            "OutForDelivery" &&
                        deliveryAssignment.Order != null &&
                        deliveryAssignment.Order.Status !=
                            "Delivered" &&
                        deliveryAssignment.Order.Status !=
                            "Cancelled");

            if (alreadyHasRunningDelivery)
            {
                TempData["Error"] =
                    "You already have an active delivery. " +
                    "Please mark the current order as delivered " +
                    "before starting another order.";

                return RedirectToPage();
            }

            if (!deliveryPerson.CurrentLatitude.HasValue ||
                !deliveryPerson.CurrentLongitude.HasValue ||
                !deliveryPerson.LastLocationUpdate.HasValue)
            {
                TempData["Error"] =
                    "Please allow GPS first. Keep the page open " +
                    "until your location appears, then click " +
                    "Start Delivery.";

                return RedirectToPage();
            }

            var lastGpsAgeSeconds =
                Math.Abs(
                    (DateTime.UtcNow -
                     deliveryPerson.LastLocationUpdate.Value)
                    .TotalSeconds);

            if (lastGpsAgeSeconds > 60)
            {
                TempData["Error"] =
                    "Your GPS is old. Please wait for location " +
                    "update, then click Start Delivery again.";

                return RedirectToPage();
            }

            assignment.Status =
                "OutForDelivery";

            assignment.PickupTime =
                DateTime.UtcNow;

            assignment.Order.Status =
                "Out for Delivery";

            var relatedNotifications =
                await _context.Notifications
                    .Where(notification =>
                        notification.UserID ==
                            deliveryPerson.UserID &&
                        notification.Type ==
                            "DeliveryAssignment" &&
                        notification.ReferenceID ==
                            assignment.OrderID &&
                        !notification.IsRead)
                    .ToListAsync();

            foreach (var notification in relatedNotifications)
            {
                notification.IsRead =
                    true;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Delivery started. Customer can now track your movement.";

            return RedirectToPage();
        }

        // =====================================================
        // MARK ORDER AS DELIVERED
        // =====================================================
        public async Task<IActionResult>
            OnPostMarkDeliveredAsync(
                int assignmentId)
        {
            var assignment =
                await GetAssignmentForCurrentDeliveryPersonAsync(
                    assignmentId);

            if (assignment == null ||
                assignment.Order == null)
            {
                TempData["Error"] =
                    "Assignment not found.";

                return RedirectToPage();
            }

            if (!string.Equals(
                    assignment.Status,
                    "OutForDelivery",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "You must start delivery before marking it delivered.";

                return RedirectToPage();
            }

            assignment.Status =
                "Delivered";

            assignment.DeliveryTime =
                DateTime.UtcNow;

            assignment.Order.Status =
                "Delivered";

            var paymentMethod =
                assignment.Order.PaymentMethod?
                    .Trim()
                ?? string.Empty;

            var isCashOnDelivery =
                paymentMethod.Equals(
                    "Cash On Delivery",
                    StringComparison.OrdinalIgnoreCase)
                ||
                paymentMethod.Equals(
                    "COD",
                    StringComparison.OrdinalIgnoreCase);

            if (isCashOnDelivery)
            {
                assignment.Order.PaymentStatus =
                    "Paid";

                var latestPayment =
                    await _context.Payments
                        .Where(payment =>
                            payment.OrderID ==
                                assignment.Order.OrderID)
                        .OrderByDescending(payment =>
                            payment.PaymentDate)
                        .FirstOrDefaultAsync();

                if (latestPayment != null)
                {
                    latestPayment.Status =
                        "Paid";

                    latestPayment.PaymentDate =
                        DateTime.UtcNow;

                    latestPayment.PaymentGateway =
                        string.IsNullOrWhiteSpace(
                            latestPayment.PaymentGateway)
                            ? "Cash"
                            : latestPayment.PaymentGateway;
                }
                else
                {
                    _context.Payments.Add(
                        new Payment
                        {
                            OrderID =
                                assignment.Order.OrderID,

                            PaymentMethod =
                                "Cash On Delivery",

                            PaymentGateway =
                                "Cash",

                            GatewayTransactionID =
                                null,

                            Amount =
                                assignment.Order.TotalAmount,

                            PaymentDate =
                                DateTime.UtcNow,

                            Status =
                                "Paid",

                            RefundAmount =
                                null,

                            RefundDate =
                                null
                        });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Order marked as delivered. Tracking stopped.";

            return RedirectToPage();
        }

        // =====================================================
        // GET ASSIGNMENT FOR LOGGED-IN DELIVERY PERSON
        // =====================================================
        private async Task<DeliveryAssignment?>
            GetAssignmentForCurrentDeliveryPersonAsync(
                int assignmentId)
        {
            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                return null;
            }

            return await _context.DeliveryAssignments
                .Include(assignment =>
                    assignment.Order)
                .FirstOrDefaultAsync(
                    assignment =>
                        assignment.AssignmentID ==
                            assignmentId &&
                        assignment.DeliveryPersonID ==
                            deliveryPerson.DeliveryPersonID);
        }

        // =====================================================
        // GET LOGGED-IN DELIVERY PERSON
        // =====================================================
        private async Task<DeliveryPerson?>
            GetCurrentDeliveryPersonAsync()
        {
            var user =
                await _userManager.GetUserAsync(
                    User);

            if (user == null)
            {
                return null;
            }

            return await _context.DeliveryPersons
                .FirstOrDefaultAsync(
                    deliveryPerson =>
                        deliveryPerson.UserID ==
                            user.Id &&
                        deliveryPerson.IsActive &&
                        deliveryPerson.Status ==
                            "Approved");
        }
    }

    public class DeliveryAssignmentViewModel
    {
        public int AssignmentID { get; set; }

        public int OrderID { get; set; }

        public string OrderNumber { get; set; }
            = string.Empty;

        public string OrderStatus { get; set; }
            = string.Empty;

        public string AssignmentStatus { get; set; }
            = string.Empty;

        public string CustomerAddress { get; set; }
            = string.Empty;

        public DateTime AssignedAt { get; set; }
    }

    public class DeliveryNotificationViewModel
    {
        public int NotificationID { get; set; }

        public string Title { get; set; }
            = string.Empty;

        public string Message { get; set; }
            = string.Empty;

        public DateTime SentAt { get; set; }

        public bool IsRead { get; set; }

        public int? ReferenceID { get; set; }
    }
}

