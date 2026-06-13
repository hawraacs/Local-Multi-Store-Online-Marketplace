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
    public class InvoiceModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public InvoiceModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Order? Order { get; set; }

        public string CustomerName { get; set; } = "Customer";

        public string AddressText { get; set; } = "No address";

        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            if (orderId <= 0)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                return Page();
            }

            Order = await _context.Orders
                .Include(o => o.Address)
                .Include(o => o.OrderItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o =>
                    o.OrderID == orderId &&
                    o.CustomerID == customer.CustomerID);

            if (Order == null)
            {
                return Page();
            }

            if (Order.PaymentStatus != "Paid" && Order.Status != "Delivered")
            {
                Order = null;
                return Page();
            }

            CustomerName = !string.IsNullOrWhiteSpace(customer.User.FullName)
                ? customer.User.FullName
                : customer.User.Email ?? "Customer";

            if (Order.Address != null)
            {
                AddressText =
                    $"{Order.Address.AddressLine1}, {Order.Address.Area}, {Order.Address.City}";
            }

            return Page();
        }
    }
}