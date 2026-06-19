using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

    public DeliveryOrdersModel(
        ApplicationDbContext context,
        UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // PAGE DATA
        // ==========================================
        public List<DeliveryOrderItemViewModel> Orders { get; set; }
            = new();

        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        // ==========================================
        // COUNTS
        // ==========================================
        public int TotalCount { get; set; }

        public int AssignedCount { get; set; }

        public int OutForDeliveryCount { get; set; }

        public int DeliveredCount { get; set; }

        public int CancelledCount { get; set; }

        // ==========================================
        // GET DELIVERY ORDERS
        // ==========================================
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            // This must use UserID, not RequestedByUserID.
            // UserID now belongs to the generated Delivery account.
            var deliveryPerson =
                await _context.DeliveryPersons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d =>
                        d.UserID == user.Id &&
                        d.IsActive &&
                        d.Status == "Approved");

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved and active delivery profile was not found.";

                Orders = new List<DeliveryOrderItemViewModel>();

                ResetCounts();

                return Page();
            }

            var assignments =
                await _context.DeliveryAssignments
                    .AsNoTracking()
                    .Include(a => a.Order)
                        .ThenInclude(o => o.Address)
                    .Where(a =>
                        a.DeliveryPersonID ==
                        deliveryPerson.DeliveryPersonID)
                    .OrderByDescending(a =>
                        a.AssignedAt)
                    .ToListAsync();

            // ==========================================
            // COUNTS
            // ==========================================
            TotalCount = assignments.Count;

            AssignedCount =
                assignments.Count(a =>
                    StatusEquals(a.Status, "Assigned"));

            OutForDeliveryCount =
                assignments.Count(a =>
                    StatusEquals(
                        a.Status,
                        "OutForDelivery") ||
                    StatusEquals(
                        a.Status,
                        "Out for Delivery"));

            DeliveredCount =
                assignments.Count(a =>
                    StatusEquals(a.Status, "Delivered"));

            CancelledCount =
                assignments.Count(a =>
                    StatusEquals(a.Status, "Cancelled") ||
                    StatusEquals(a.Status, "Failed"));

            // ==========================================
            // FILTER
            // ==========================================
            IEnumerable<DeliveryAssignment> filteredAssignments =
                assignments;

            if (!string.IsNullOrWhiteSpace(StatusFilter) &&
                !StatusEquals(StatusFilter, "All"))
            {
                if (StatusEquals(
                        StatusFilter,
                        "Cancelled"))
                {
                    filteredAssignments =
                        filteredAssignments.Where(a =>
                            StatusEquals(
                                a.Status,
                                "Cancelled") ||
                            StatusEquals(
                                a.Status,
                                "Failed"));
                }
                else if (
                    StatusEquals(
                        StatusFilter,
                        "OutForDelivery") ||
                    StatusEquals(
                        StatusFilter,
                        "Out for Delivery"))
                {
                    filteredAssignments =
                        filteredAssignments.Where(a =>
                            StatusEquals(
                                a.Status,
                                "OutForDelivery") ||
                            StatusEquals(
                                a.Status,
                                "Out for Delivery"));
                }
                else
                {
                    filteredAssignments =
                        filteredAssignments.Where(a =>
                            StatusEquals(
                                a.Status,
                                StatusFilter));
                }
            }

            // ==========================================
            // BUILD VIEW MODEL
            // ==========================================
            Orders = filteredAssignments
                .Select(a =>
                    new DeliveryOrderItemViewModel
                    {
                        AssignmentID =
                            a.AssignmentID,

                        OrderID =
                            a.OrderID,

                        OrderNumber =
                            a.Order != null &&
                            !string.IsNullOrWhiteSpace(
                                a.Order.OrderNumber)
                                ? a.Order.OrderNumber
                                : "N/A",

                        OrderStatus =
                            a.Order != null &&
                            !string.IsNullOrWhiteSpace(
                                a.Order.Status)
                                ? a.Order.Status
                                : "N/A",

                        AssignmentStatus =
                            !string.IsNullOrWhiteSpace(a.Status)
                                ? a.Status
                                : "N/A",

                        CustomerAddress =
                            BuildCustomerAddress(
                                a.Order?.Address),

                        PaymentMethod =
                            a.Order != null &&
                            !string.IsNullOrWhiteSpace(
                                a.Order.PaymentMethod)
                                ? a.Order.PaymentMethod
                                : "N/A",

                        PaymentStatus =
                            a.Order != null &&
                            !string.IsNullOrWhiteSpace(
                                a.Order.PaymentStatus)
                                ? a.Order.PaymentStatus
                                : "N/A",

                        TotalAmount =
                            a.Order?.TotalAmount ?? 0,

                        AssignedAt =
                            a.AssignedAt,

                        PickupTime =
                            a.PickupTime,

                        DeliveryTime =
                            a.DeliveryTime
                    })
                .ToList();

            return Page();
        }

        // ==========================================
        // BUILD CUSTOMER ADDRESS
        // ==========================================
        private static string BuildCustomerAddress(
            CustomerAddress? address)
        {
            if (address == null)
            {
                return "No address available";
            }

            var addressParts = new[]
            {
            address.AddressLine1,
            address.Area,
            address.City
        }
            .Where(part =>
                !string.IsNullOrWhiteSpace(part))
            .Select(part =>
                part!.Trim())
            .ToList();

            return addressParts.Count > 0
                ? string.Join(", ", addressParts)
                : "No address available";
        }

        // ==========================================
        // RESET COUNTS
        // ==========================================
        private void ResetCounts()
        {
            TotalCount = 0;
            AssignedCount = 0;
            OutForDeliveryCount = 0;
            DeliveredCount = 0;
            CancelledCount = 0;
        }

        // ==========================================
        // CASE-INSENSITIVE STATUS CHECK
        // ==========================================
        private static bool StatusEquals(
            string? currentStatus,
            string? expectedStatus)
        {
            return string.Equals(
                currentStatus?.Trim(),
                expectedStatus?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        // ==========================================
        // FORMAT DATE FOR DISPLAY
        // ==========================================
        public string FormatLocalTime(DateTime? value)
        {
            if (!value.HasValue)
            {
                return "Not available";
            }

            var dateValue = value.Value;

            var utcValue =
                dateValue.Kind == DateTimeKind.Utc
                    ? dateValue
                    : DateTime.SpecifyKind(
                        dateValue,
                        DateTimeKind.Utc);

            return utcValue
                .ToLocalTime()
                .ToString("dd MMM yyyy - HH:mm");
        }
    }

    // ==========================================
    // DELIVERY ORDER VIEW MODEL
    // ==========================================
    public class DeliveryOrderItemViewModel
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

        public string PaymentMethod { get; set; }
            = string.Empty;

        public string PaymentStatus { get; set; }
            = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime AssignedAt { get; set; }

        public DateTime? PickupTime { get; set; }

        public DateTime? DeliveryTime { get; set; }
    }
}
