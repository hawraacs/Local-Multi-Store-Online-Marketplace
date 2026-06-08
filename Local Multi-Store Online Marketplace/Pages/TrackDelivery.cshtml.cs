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
                .FirstOrDefaultAsync(o =>
                    o.OrderID == OrderId &&
                    o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToPage("/CustomerOrders");
            }

            if (order.Status != "Out for Delivery" &&
                order.Status != "OutForDelivery" &&
                order.Status != "Delivered")
            {
                TempData["Error"] =
                    "Tracking is available only when the order is out for delivery.";

                return RedirectToPage("/CustomerOrders");
            }

            OrderNumber = order.OrderNumber;
            OrderStatus = order.Status;

            return Page();
        }
    }
}