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

        public List<DeliveryOrderItemViewModel> Orders { get; set; } = new();

        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        public int TotalCount { get; set; }

        public int AssignedCount { get; set; }

        public int OutForDeliveryCount { get; set; }

        public int DeliveredCount { get; set; }

        public int CancelledCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.UserID == user.Id &&
                    d.Status == "Approved");

            if (deliveryPerson == null)
            {
                ErrorMessage = "Approved delivery profile was not found.";
                Orders = new List<DeliveryOrderItemViewModel>();
                return Page();
            }

            var assignments = await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o.Address)
                .Where(a =>
                    a.DeliveryPersonID == deliveryPerson.DeliveryPersonID)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            TotalCount = assignments.Count;

            AssignedCount = assignments.Count(a =>
                a.Status == "Assigned");

            OutForDeliveryCount = assignments.Count(a =>
                a.Status == "OutForDelivery");

            DeliveredCount = assignments.Count(a =>
                a.Status == "Delivered");

            CancelledCount = assignments.Count(a =>
                a.Status == "Cancelled" ||
                a.Status == "Failed");

            var filteredAssignments = assignments.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(StatusFilter) &&
                !StatusFilter.Equals(
                    "All",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (StatusFilter.Equals(
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
                {
                    filteredAssignments = filteredAssignments.Where(a =>
                        a.Status == "Cancelled" ||
                        a.Status == "Failed");
                }
                else
                {
                    filteredAssignments = filteredAssignments.Where(a =>
                        string.Equals(
                            a.Status,
                            StatusFilter,
                            StringComparison.OrdinalIgnoreCase));
                }
            }

            Orders = filteredAssignments
                .Select(a => new DeliveryOrderItemViewModel
                {
                    AssignmentID = a.AssignmentID,
                    OrderID = a.OrderID,

                    OrderNumber = a.Order != null
                        ? a.Order.OrderNumber
                        : "N/A",

                    OrderStatus = a.Order != null
                        ? a.Order.Status
                        : "N/A",

                    AssignmentStatus = a.Status,

                    CustomerAddress =
                        a.Order != null &&
                        a.Order.Address != null
                            ? $"{a.Order.Address.AddressLine1}, " +
                              $"{a.Order.Address.Area}, " +
                              $"{a.Order.Address.City}"
                            : "No address available",

                    PaymentMethod = a.Order != null
                        ? a.Order.PaymentMethod
                        : "N/A",

                    PaymentStatus = a.Order != null
                        ? a.Order.PaymentStatus
                        : "N/A",

                    TotalAmount = a.Order != null
                        ? a.Order.TotalAmount
                        : 0,

                    AssignedAt = a.AssignedAt,
                    PickupTime = a.PickupTime,
                    DeliveryTime = a.DeliveryTime
                })
                .ToList();

            return Page();
        }

        public string FormatLocalTime(DateTime? value)
        {
            if (!value.HasValue)
            {
                return "Not available";
            }

            var utcValue = DateTime.SpecifyKind(
                value.Value,
                DateTimeKind.Utc);

            return utcValue
                .ToLocalTime()
                .ToString("dd MMM yyyy - HH:mm");
        }
    }

    public class DeliveryOrderItemViewModel
    {
        public int AssignmentID { get; set; }

        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string OrderStatus { get; set; } = string.Empty;

        public string AssignmentStatus { get; set; } = string.Empty;

        public string CustomerAddress { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime AssignedAt { get; set; }

        public DateTime? PickupTime { get; set; }

        public DateTime? DeliveryTime { get; set; }
    }
}