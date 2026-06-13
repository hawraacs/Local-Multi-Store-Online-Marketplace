using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CustomerOrdersModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<CustomerOrderViewModel> Orders { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile was not found.";
                Orders = new List<CustomerOrderViewModel>();
                return Page();
            }

            Orders = await _context.Orders
                .Where(o => o.CustomerID == customer.CustomerID)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new CustomerOrderViewModel
                {
                    OrderID = o.OrderID,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    TotalAmount = o.TotalAmount,

                    AssignmentStatus = _context.DeliveryAssignments
                        .Where(a =>
                            a.OrderID == o.OrderID &&
                            a.Status != "Cancelled" &&
                            a.Status != "Failed")
                        .Select(a => a.Status)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Page();
        }
    }

    public class CustomerOrderViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public string? AssignmentStatus { get; set; }

        public bool HasDeliveryAssignment =>
            !string.IsNullOrWhiteSpace(AssignmentStatus);

        public bool CanTrack
        {
            get
            {
                var cleanOrderStatus = Status?.Trim();
                var cleanAssignmentStatus = AssignmentStatus?.Trim();

                return cleanOrderStatus == "Out for Delivery" &&
                       cleanAssignmentStatus == "OutForDelivery";
            }
        }

        public string TrackingMessage
        {
            get
            {
                var cleanStatus = Status?.Trim();

                if (cleanStatus == "Assigned")
                    return "Available when delivery starts";

                if (cleanStatus == "Delivered")
                    return "Delivered";

                if (cleanStatus == "Out for Delivery")
                    return "Track Delivery";

                return "Available when out for delivery";
            }
        }

        public bool CanViewInvoice
        {
            get
            {
                var cleanPayment = PaymentStatus?.Trim();

                return cleanPayment == "Paid" ||
                       Status == "Delivered";
            }
        }
    }
}