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
    public class TrackDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TrackDeliveryModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public int OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string OrderStatus { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile was not found.";
                return RedirectToPage("/CustomerOrders");
            }

            var order = await _context.Orders
                .Include(o => o.DeliveryAssignment)
                .FirstOrDefaultAsync(o =>
                    o.OrderID == OrderId &&
                    o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToPage("/CustomerOrders");
            }

            if (order.DeliveryAssignment == null)
            {
                TempData["Error"] = "Delivery has not been assigned yet.";
                return RedirectToPage("/CustomerOrders");
            }

            var cleanOrderStatus = order.Status?.Trim();
            var cleanAssignmentStatus = order.DeliveryAssignment.Status?.Trim();

            var canTrackLive =
                cleanOrderStatus == "Out for Delivery" &&
                cleanAssignmentStatus == "OutForDelivery";

            var canTrackDelivered =
                cleanOrderStatus == "Delivered" &&
                cleanAssignmentStatus == "Delivered";

            if (!canTrackLive && !canTrackDelivered)
            {
                TempData["Error"] = "Tracking is available only after the delivery person starts delivery.";
                return RedirectToPage("/CustomerOrders");
            }

            OrderNumber = order.OrderNumber;
            OrderStatus = order.Status;

            return Page();
        }
    }
}