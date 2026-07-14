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
    public class PaymentSuccessModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public PaymentSuccessModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Order? Order { get; set; }

        public string? TransactionId { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int orderId, string? transactionId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                ErrorMessage = "Customer profile was not found.";
                return Page();
            }

            var order = await _context.Orders
                .Include(o => o.Payments)
                .AsNoTracking()
                .FirstOrDefaultAsync(o =>
                    o.OrderID == orderId &&
                    o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                ErrorMessage = "Order not found or you are not allowed to access it.";
                return Page();
            }

            // Guard against navigating here directly without an
            // actual completed payment.
            if (!order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/OnlinePayment", new { orderId = order.OrderID });
            }

            Order = order;

            TransactionId = transactionId
                ?? order.Payments
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefault()?.GatewayTransactionID;

            return Page();
        }
    }
}
