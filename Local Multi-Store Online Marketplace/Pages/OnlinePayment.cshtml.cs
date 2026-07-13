using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Stripe;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class OnlinePaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OnlinePaymentModel> _logger;

        public OnlinePaymentModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IConfiguration configuration,
            ILogger<OnlinePaymentModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        public Order? Order { get; set; }

        public string? ErrorMessage { get; set; }

        // Exposed to the view so Stripe.js can initialize client-side.
        // This is the publishable key — safe to expose in the browser.
        public string StripePublishableKey =>
            _configuration["Stripe:PublishableKey"] ?? string.Empty;

        [BindProperty]
        public int OrderId { get; set; }

        // Populated client-side by Stripe.js after it tokenizes the
        // card entered into the Stripe Card Element. The raw card
        // number/expiry/CVC are never sent to or seen by this server —
        // only this opaque PaymentMethod ID is.
        [BindProperty]
        public string StripePaymentMethodId { get; set; } = string.Empty;

        // =====================================================
        // OPEN PAYMENT PAGE
        // =====================================================
        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            OrderId = orderId;

            var order = await LoadOrderForCurrentCustomerAsync(orderId);

            if (order == null)
            {
                ErrorMessage =
                    "Payment order was not found or you are not allowed to access it.";

                return Page();
            }

            Order = order;

            var validationError = ValidateOrderForPayment(order);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                if (order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = "This order is already paid.";
                    return RedirectToPage("/CustomerOrders");
                }

                ErrorMessage = validationError;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(StripePublishableKey) ||
                StripePublishableKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage =
                    "Online payment is not configured yet. Please contact support.";
            }

            return Page();
        }

        // =====================================================
        // PROCESS ONLINE PAYMENT (real Stripe PaymentIntent)
        // =====================================================
        public async Task<IActionResult> OnPostAsync()
        {
            var order = await LoadOrderForCurrentCustomerAsync(OrderId);

            if (order == null)
            {
                ErrorMessage =
                    "Payment order was not found or you are not allowed to access it.";

                return Page();
            }

            Order = order;

            var orderValidationError = ValidateOrderForPayment(order);

            if (!string.IsNullOrWhiteSpace(orderValidationError))
            {
                if (order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = "This order is already paid.";
                    return RedirectToPage("/CustomerOrders");
                }

                ErrorMessage = orderValidationError;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(StripePaymentMethodId))
            {
                ErrorMessage = "Enter your card details before paying.";
                return Page();
            }

            var secretKey = _configuration["Stripe:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey) ||
                secretKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage =
                    "Online payment is not configured yet. Please contact support.";
                return Page();
            }

            StripeConfiguration.ApiKey = secretKey;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            PaymentIntent intent;

            try
            {
                var paymentIntentService = new PaymentIntentService();

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)Math.Round(order.TotalAmount * 100m, 0),
                    Currency = "usd",
                    PaymentMethod = StripePaymentMethodId,
                    Confirm = true,
                    PaymentMethodTypes = new List<string> { "card" },
                    Description = $"Order {order.OrderNumber}",
                    // No 3D Secure redirect flow in this simplified
                    // integration — a card that requires additional
                    // authentication is treated as a failed attempt
                    // below rather than sent through a challenge step.
                    ErrorOnRequiresAction = true
                };

                intent = await paymentIntentService.CreateAsync(options);
            }
            catch (StripeException exception)
            {
                await transaction.RollbackAsync();

                _logger.LogWarning(
                    exception,
                    "Stripe payment declined or failed for order {OrderId}.",
                    OrderId);

                return RedirectToPage("/PaymentFailed", new
                {
                    orderId = order.OrderID,
                    reason = exception.StripeError?.Message
                        ?? "The card was declined. No amount was charged."
                });
            }

            if (intent.Status != "succeeded")
            {
                await transaction.RollbackAsync();

                _logger.LogWarning(
                    "Stripe PaymentIntent {IntentId} for order {OrderId} ended with status {Status}.",
                    intent.Id,
                    OrderId,
                    intent.Status);

                return RedirectToPage("/PaymentFailed", new
                {
                    orderId = order.OrderID,
                    reason = "Payment could not be completed. No amount was charged."
                });
            }

            try
            {
                var previousOrderStatus = order.Status;
                var currentOrderStatus = order.Status?.Trim() ?? string.Empty;

                order.PaymentStatus = "Paid";

                if (currentOrderStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
                    currentOrderStatus.Equals("Pending Confirmation", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Confirmed";
                }

                var latestPayment = await _context.Payments
                    .Where(payment => payment.OrderID == order.OrderID)
                    .OrderByDescending(payment => payment.PaymentDate)
                    .FirstOrDefaultAsync();

                if (latestPayment != null)
                {
                    latestPayment.PaymentMethod = "Online Payment";
                    latestPayment.PaymentGateway = "Stripe";
                    latestPayment.GatewayTransactionID = intent.Id;
                    latestPayment.Amount = order.TotalAmount;
                    latestPayment.PaymentDate = DateTime.UtcNow;
                    latestPayment.Status = "Paid";
                    latestPayment.RefundAmount = null;
                    latestPayment.RefundDate = null;
                }
                else
                {
                    _context.Payments.Add(new Payment
                    {
                        OrderID = order.OrderID,
                        PaymentMethod = "Online Payment",
                        PaymentGateway = "Stripe",
                        GatewayTransactionID = intent.Id,
                        Amount = order.TotalAmount,
                        PaymentDate = DateTime.UtcNow,
                        Status = "Paid",
                        RefundAmount = null,
                        RefundDate = null
                    });
                }

                if (!string.Equals(previousOrderStatus, order.Status, StringComparison.OrdinalIgnoreCase))
                {
                    _context.OrderStatusHistories.Add(new OrderStatusHistory
                    {
                        OrderID = order.OrderID,
                        PreviousStatus = previousOrderStatus,
                        NewStatus = order.Status,
                        ChangedBy = User.Identity?.Name ?? "Customer",
                        ChangedAt = DateTime.UtcNow,
                        Notes = $"Order confirmed after successful Stripe payment ({intent.Id})."
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToPage("/PaymentSuccess", new
                {
                    orderId = order.OrderID,
                    transactionId = intent.Id
                });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                // The Stripe charge itself already succeeded at this
                // point; this catch only covers our own DB write
                // failing afterward. Logged as an error, not a normal
                // decline, since money was actually captured.
                _logger.LogError(
                    exception,
                    "Order update failed after a successful Stripe charge ({IntentId}) for order {OrderId}.",
                    intent.Id,
                    OrderId);

                return RedirectToPage("/PaymentFailed", new
                {
                    orderId = order.OrderID,
                    reason = "Your payment was charged, but we could not update your order. Please contact support with reference " + intent.Id + "."
                });
            }
        }

        // =====================================================
        // VALIDATE ORDER
        // =====================================================
        private static string? ValidateOrderForPayment(Order order)
        {
            var paymentMethod = order.PaymentMethod?.Trim() ?? string.Empty;
            var paymentStatus = order.PaymentStatus?.Trim() ?? string.Empty;
            var orderStatus = order.Status?.Trim() ?? string.Empty;

            if (!paymentMethod.Equals("Online Payment", StringComparison.OrdinalIgnoreCase))
            {
                return "This order does not use online payment.";
            }

            if (paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                return "This order is already paid.";
            }

            if (orderStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return "A cancelled order cannot be paid.";
            }

            if (orderStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
            {
                return "This order is already completed.";
            }

            if (order.TotalAmount <= 0)
            {
                return "The order total is invalid and cannot be paid.";
            }

            return null;
        }

        // =====================================================
        // LOAD ORDER FOR CURRENT CUSTOMER
        // =====================================================
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
                .AsNoTracking()
                .FirstOrDefaultAsync(customer => customer.UserID == user.Id);

            if (customer == null)
            {
                return null;
            }

            return await _context.Orders
                .Include(order => order.Payments)
                .FirstOrDefaultAsync(order =>
                    order.OrderID == orderId &&
                    order.CustomerID == customer.CustomerID);
        }
    }
}
