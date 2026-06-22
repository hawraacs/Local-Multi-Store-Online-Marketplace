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
        private readonly ILogger<OnlinePaymentModel> _logger;

        public OnlinePaymentModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<OnlinePaymentModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
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

        // =====================================================
        // OPEN PAYMENT PAGE
        // =====================================================
        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            OrderId = orderId;

            var order =
                await LoadOrderForCurrentCustomerAsync(orderId);

            if (order == null)
            {
                ErrorMessage =
                    "Payment order was not found or you are not allowed to access it.";

                return Page();
            }

            Order = order;

            var validationError =
                ValidateOrderForPayment(order);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                if (order.PaymentStatus.Equals(
                        "Paid",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] =
                        "This order is already paid.";

                    return RedirectToPage("/CustomerOrders");
                }

                ErrorMessage =
                    validationError;

                return Page();
            }

            return Page();
        }

        // =====================================================
        // PROCESS ACADEMIC ONLINE PAYMENT
        // =====================================================
        public async Task<IActionResult> OnPostAsync()
        {
            var order =
                await LoadOrderForCurrentCustomerAsync(OrderId);

            if (order == null)
            {
                ErrorMessage =
                    "Payment order was not found or you are not allowed to access it.";

                return Page();
            }

            Order = order;

            var orderValidationError =
                ValidateOrderForPayment(order);

            if (!string.IsNullOrWhiteSpace(orderValidationError))
            {
                if (order.PaymentStatus.Equals(
                        "Paid",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] =
                        "This order is already paid.";

                    return RedirectToPage("/CustomerOrders");
                }

                ErrorMessage =
                    orderValidationError;

                return Page();
            }

            NormalizePaymentInput();

            var cardValidationError =
                ValidateCardInput();

            if (!string.IsNullOrWhiteSpace(cardValidationError))
            {
                ErrorMessage =
                    cardValidationError;

                return Page();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                /*
                 * Academic payment simulation:
                 * No card number or CVV is stored.
                 * No external bank is contacted.
                 */
                var transactionId =
                    $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}-" +
                    $"{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

                var previousOrderStatus =
                    order.Status;

                var currentOrderStatus =
                    order.Status?.Trim()
                    ?? string.Empty;

                order.PaymentStatus =
                    "Paid";

                if (currentOrderStatus.Equals(
                        "Pending",
                        StringComparison.OrdinalIgnoreCase) ||
                    currentOrderStatus.Equals(
                        "Pending Confirmation",
                        StringComparison.OrdinalIgnoreCase))
                {
                    order.Status =
                        "Confirmed";
                }

                var latestPayment =
                    await _context.Payments
                        .Where(payment =>
                            payment.OrderID == order.OrderID)
                        .OrderByDescending(payment =>
                            payment.PaymentDate)
                        .FirstOrDefaultAsync();

                if (latestPayment != null)
                {
                    latestPayment.PaymentMethod =
                        "Online Payment";

                    latestPayment.PaymentGateway =
                        "Academic Test Gateway";

                    latestPayment.GatewayTransactionID =
                        transactionId;

                    latestPayment.Amount =
                        order.TotalAmount;

                    latestPayment.PaymentDate =
                        DateTime.UtcNow;

                    latestPayment.Status =
                        "Paid";

                    latestPayment.RefundAmount =
                        null;

                    latestPayment.RefundDate =
                        null;
                }
                else
                {
                    _context.Payments.Add(
                        new Payment
                        {
                            OrderID =
                                order.OrderID,

                            PaymentMethod =
                                "Online Payment",

                            PaymentGateway =
                                "Academic Test Gateway",

                            GatewayTransactionID =
                                transactionId,

                            Amount =
                                order.TotalAmount,

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

                if (!string.Equals(
                        previousOrderStatus,
                        order.Status,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _context.OrderStatusHistories.Add(
                        new OrderStatusHistory
                        {
                            OrderID =
                                order.OrderID,

                            PreviousStatus =
                                previousOrderStatus,

                            NewStatus =
                                order.Status,

                            ChangedBy =
                                User.Identity?.Name
                                ?? "Customer",

                            ChangedAt =
                                DateTime.UtcNow,

                            Notes =
                                "Order confirmed after successful academic online payment."
                        });
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] =
                    $"Online payment completed successfully. " +
                    $"Transaction ID: {transactionId}.";

                return RedirectToPage("/CustomerOrders");
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    exception,
                    "Academic online payment failed for order {OrderId}.",
                    OrderId);

                ErrorMessage =
                    "Payment processing could not be completed. " +
                    "No payment changes were saved. Please try again.";

                return Page();
            }
        }

        // =====================================================
        // VALIDATE ORDER
        // =====================================================
        private static string?
            ValidateOrderForPayment(
                Order order)
        {
            var paymentMethod =
                order.PaymentMethod?.Trim()
                ?? string.Empty;

            var paymentStatus =
                order.PaymentStatus?.Trim()
                ?? string.Empty;

            var orderStatus =
                order.Status?.Trim()
                ?? string.Empty;

            if (!paymentMethod.Equals(
                    "Online Payment",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "This order does not use online payment.";
            }

            if (paymentStatus.Equals(
                    "Paid",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "This order is already paid.";
            }

            if (orderStatus.Equals(
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "A cancelled order cannot be paid.";
            }

            if (orderStatus.Equals(
                    "Delivered",
                    StringComparison.OrdinalIgnoreCase))
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
        private async Task<Order?>
            LoadOrderForCurrentCustomerAsync(
                int orderId)
        {
            if (orderId <= 0)
            {
                return null;
            }

            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            var customer =
                await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(customer =>
                        customer.UserID == user.Id);

            if (customer == null)
            {
                return null;
            }

            return await _context.Orders
                .Include(order =>
                    order.Payments)
                .FirstOrDefaultAsync(order =>
                    order.OrderID == orderId &&
                    order.CustomerID ==
                    customer.CustomerID);
        }

        // =====================================================
        // NORMALIZE INPUT
        // =====================================================
        private void NormalizePaymentInput()
        {
            CardholderName =
                CardholderName?
                    .Trim()
                ?? string.Empty;

            CardNumber =
                CardNumber?
                    .Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Trim()
                ?? string.Empty;

            ExpiryDate =
                ExpiryDate?
                    .Trim()
                ?? string.Empty;

            Cvv =
                Cvv?
                    .Trim()
                ?? string.Empty;
        }

        // =====================================================
        // VALIDATE CARD INPUT
        // =====================================================
        private string? ValidateCardInput()
        {
            if (string.IsNullOrWhiteSpace(
                    CardholderName))
            {
                return "Cardholder name is required.";
            }

            if (CardholderName.Length < 3)
            {
                return "Cardholder name is too short.";
            }

            if (CardholderName.Length > 100)
            {
                return "Cardholder name is too long.";
            }

            if (string.IsNullOrWhiteSpace(
                    CardNumber) ||
                !CardNumber.All(char.IsDigit))
            {
                return "Card number must contain digits only.";
            }

            if (CardNumber.Length != 16)
            {
                return "Card number must contain exactly 16 digits.";
            }

            var cardBrand =
                GetCardBrand(CardNumber);

            if (cardBrand == "Unsupported")
            {
                return "Only 16-digit Visa and Mastercard test cards are supported.";
            }

            if (!IsValidLuhn(CardNumber))
            {
                return "The card number is not valid. Please check the digits and try again.";
            }

            if (string.IsNullOrWhiteSpace(
                    ExpiryDate) ||
                ExpiryDate.Length != 5 ||
                ExpiryDate[2] != '/')
            {
                return "Expiry date must use the MM/YY format.";
            }

            var expiryParts =
                ExpiryDate.Split('/');

            if (expiryParts.Length != 2 ||
                !int.TryParse(
                    expiryParts[0],
                    out var month) ||
                !int.TryParse(
                    expiryParts[1],
                    out var shortYear))
            {
                return "Expiry date is invalid.";
            }

            if (month < 1 ||
                month > 12)
            {
                return "Expiry month must be between 01 and 12.";
            }

            var currentYear =
                DateTime.UtcNow.Year % 100;

            var currentMonth =
                DateTime.UtcNow.Month;

            if (shortYear < currentYear ||
                (shortYear == currentYear &&
                 month < currentMonth))
            {
                return "The card has expired.";
            }

            if (string.IsNullOrWhiteSpace(
                    Cvv) ||
                Cvv.Length != 3 ||
                !Cvv.All(char.IsDigit))
            {
                return "CVV must contain exactly 3 digits.";
            }

            /*
             * Academic declined-payment scenario.
             * This number passes Luhn but is intentionally rejected.
             */
            if (CardNumber ==
                "4000000000000002")
            {
                return "Payment was declined by the academic test gateway. " +
                       "No amount was charged.";
            }

            return null;
        }

        // =====================================================
        // LUHN CHECK
        // =====================================================
        private static bool IsValidLuhn(
            string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(
                    cardNumber))
            {
                return false;
            }

            var sum =
                0;

            var doubleDigit =
                false;

            for (var index =
                    cardNumber.Length - 1;
                 index >= 0;
                 index--)
            {
                if (!char.IsDigit(
                        cardNumber[index]))
                {
                    return false;
                }

                var digit =
                    cardNumber[index] - '0';

                if (doubleDigit)
                {
                    digit *= 2;

                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;

                doubleDigit =
                    !doubleDigit;
            }

            return sum % 10 == 0;
        }

        // =====================================================
        // CARD BRAND
        // =====================================================
        private static string GetCardBrand(
            string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(
                    cardNumber) ||
                cardNumber.Length != 16)
            {
                return "Unsupported";
            }

            if (cardNumber.StartsWith(
                    "4",
                    StringComparison.Ordinal))
            {
                return "Visa";
            }

            if (int.TryParse(
                    cardNumber[..2],
                    out var prefix) &&
                prefix >= 51 &&
                prefix <= 55)
            {
                return "Mastercard";
            }

            return "Unsupported";
        }
    }
}
