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
    public class OnlinePaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public OnlinePaymentModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Order? Order { get; set; }

        public string? ErrorMessage { get; set; }

        [BindProperty]
        public int OrderId { get; set; }

        [BindProperty]
        public string CardholderName { get; set; } = string.Empty;

        [BindProperty]
        public string CardNumber { get; set; } = string.Empty;

        [BindProperty]
        public string ExpiryDate { get; set; } = string.Empty;

        [BindProperty]
        public string Cvv { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            OrderId = orderId;

            var order = await LoadOrderForCurrentCustomerAsync(orderId);

            if (order == null)
            {
                ErrorMessage = "Payment order not found.";
                return Page();
            }

            Order = order;

            var paymentMethod = Order.PaymentMethod?.Trim() ?? string.Empty;
            var paymentStatus = Order.PaymentStatus?.Trim() ?? string.Empty;

            if (!paymentMethod.Equals("Online Payment", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "This order does not use online payment.";
                Order = null;
                return Page();
            }

            if (paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "This order is already paid.";
                return RedirectToPage("/CustomerOrders");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var order = await LoadOrderForCurrentCustomerAsync(OrderId);

            if (order == null)
            {
                ErrorMessage = "Payment order not found.";
                return Page();
            }

            Order = order;

            var paymentMethod = order.PaymentMethod?.Trim() ?? string.Empty;
            var paymentStatus = order.PaymentStatus?.Trim() ?? string.Empty;

            if (!paymentMethod.Equals("Online Payment", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "This order does not use online payment.";
                return Page();
            }

            if (paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "This order is already paid.";
                return RedirectToPage("/CustomerOrders");
            }

            var validationError = ValidateCardInput();

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                ErrorMessage = validationError;
                return Page();
            }

            var transactionId =
                $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            order.PaymentStatus = "Paid";

            if ((order.Status?.Trim() ?? string.Empty).Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "Confirmed";
            }

            var latestPayment = await _context.Payments
                .Where(p => p.OrderID == order.OrderID)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();

            if (latestPayment != null)
            {
                latestPayment.Status = "Paid";
                latestPayment.PaymentGateway = "Simulated Gateway";
                latestPayment.GatewayTransactionID = transactionId;
                latestPayment.PaymentDate = DateTime.UtcNow;
            }
            else
            {
                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentMethod = "Online Payment",
                    PaymentGateway = "Simulated Gateway",
                    GatewayTransactionID = transactionId,
                    Amount = order.TotalAmount,
                    PaymentDate = DateTime.UtcNow,
                    Status = "Paid",
                    RefundAmount = null,
                    RefundDate = null
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Online payment completed successfully. Transaction ID: {transactionId}.";

            return RedirectToPage("/CustomerOrders");
        }

        private async Task<Order?> LoadOrderForCurrentCustomerAsync(int orderId)
        {
            if (orderId <= 0)
            {
                return null;
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                return null;
            }

            return await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o =>
                    o.OrderID == orderId &&
                    o.CustomerID == customer.CustomerID);
        }

        private string? ValidateCardInput()
        {
            if (string.IsNullOrWhiteSpace(CardholderName))
            {
                return "Cardholder name is required.";
            }

            if (CardholderName.Trim().Length < 3)
            {
                return "Cardholder name is too short.";
            }

            var cleanCard = CardNumber
                .Replace(" ", "")
                .Replace("-", "")
                .Trim();

            if (cleanCard.Length != 16 || !cleanCard.All(char.IsDigit))
            {
                return "Card number must be 16 digits.";
            }

            var acceptedSuccessCards = new List<string>
            {
                "4242424242424242",
                "5555555555554444"
            };

            var declinedCards = new List<string>
            {
                "4000000000000002"
            };

            if (declinedCards.Contains(cleanCard))
            {
                return "Payment was declined by the demo gateway.";
            }

            if (!acceptedSuccessCards.Contains(cleanCard))
            {
                return "Invalid demo card. Please use one of the accepted test cards.";
            }

            if (string.IsNullOrWhiteSpace(ExpiryDate) ||
                ExpiryDate.Length != 5 ||
                ExpiryDate[2] != '/')
            {
                return "Expiry date must be in MM/YY format.";
            }

            var parts = ExpiryDate.Split('/');

            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var month) ||
                !int.TryParse(parts[1], out var year))
            {
                return "Invalid expiry date.";
            }

            if (month < 1 || month > 12)
            {
                return "Expiry month must be between 01 and 12.";
            }

            var currentYear = DateTime.UtcNow.Year % 100;
            var currentMonth = DateTime.UtcNow.Month;

            if (year < currentYear || (year == currentYear && month < currentMonth))
            {
                return "Card is expired.";
            }

            if (string.IsNullOrWhiteSpace(Cvv) ||
                Cvv.Length != 3 ||
                !Cvv.All(char.IsDigit))
            {
                return "CVV must be 3 digits.";
            }

            return null;
        }
    }
}