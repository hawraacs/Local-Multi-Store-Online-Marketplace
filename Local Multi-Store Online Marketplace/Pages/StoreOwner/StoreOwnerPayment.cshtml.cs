using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Stripe;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class StoreOwnerPaymentModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<StoreOwnerPaymentModel> _logger;
        private readonly IConfiguration _configuration;

        public StoreOwnerPaymentModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<StoreOwnerPaymentModel> logger,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        public StorePayment? Payment { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal CurrentBalance { get; set; } = -1; // -1 = unavailable

        [BindProperty]
        public int PaymentId { get; set; }

        [BindProperty]
        public string CardholderName { get; set; } = string.Empty;

        [BindProperty]
        public string CardNumber { get; set; } = string.Empty;

        [BindProperty]
        public string ExpiryDate { get; set; } = string.Empty;

        [BindProperty]
        public string Cvv { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int paymentId, string? returnUrl = null)
        {
            PaymentId = paymentId;
            ReturnUrl = returnUrl;

            var payment = await LoadPaymentForCurrentStoreOwnerAsync(paymentId);
            if (payment == null)
            {
                ErrorMessage = "Payment request not found or you are not authorized.";
                return Page();
            }

            Payment = payment;

            if (payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "This payment has already been completed.";
                if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl);
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            // If no Stripe account is linked, set a dummy balance for demo
            if (string.IsNullOrWhiteSpace(payment.Store.StripeAccountId))
            {
                // For demo purposes, treat as having sufficient balance
                CurrentBalance = 9999; // dummy positive balance
            }
            else
            {
                await FetchStripeBalance(payment.Store.StripeAccountId);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var payment = await LoadPaymentForCurrentStoreOwnerAsync(PaymentId);
            if (payment == null)
            {
                ErrorMessage = "Payment request not found or you are not authorized.";
                return Page();
            }

            Payment = payment;

            if (payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "This payment is already completed.";
                if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl);
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            NormalizePaymentInput();

            var cardValidationError = ValidateCardInput();
            if (!string.IsNullOrWhiteSpace(cardValidationError))
            {
                ErrorMessage = cardValidationError;
                return Page();
            }

            // Check balance - if no Stripe account, assume sufficient
            bool balanceSufficient;
            if (string.IsNullOrWhiteSpace(payment.Store.StripeAccountId))
            {
                balanceSufficient = true; // bypass for demo
            }
            else
            {
                balanceSufficient = await CheckStripeBalance(payment.Store.StripeAccountId, payment.Amount);
            }

            if (!balanceSufficient)
            {
                ErrorMessage = "Insufficient funds in your Stripe account. Please add funds and try again.";
                return Page();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var transferId = $"TRANSFER-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

                // 1. Mark payment as paid
                payment.Status = "Paid";
                payment.PaidAt = DateTime.UtcNow;
                payment.StripeTransferId = transferId;

                // 2. If this is a subscription payment, update store and create SubscriptionPayment record
                bool isSubscriptionPayment = payment.Description?.Equals("Monthly Subscription Fee", StringComparison.OrdinalIgnoreCase) == true;

                if (isSubscriptionPayment)
                {
                    var store = payment.Store;

                    // Extend subscription by 1 month
                    DateTime newExpiry = DateTime.UtcNow.AddMonths(1);
                    if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate.Value > DateTime.UtcNow)
                    {
                        newExpiry = store.SubscriptionExpiryDate.Value.AddMonths(1);
                    }

                    store.SubscriptionStatus = "Active";
                    store.SubscriptionExpiryDate = newExpiry;
                    store.LastPaymentDate = DateTime.UtcNow;
                    store.LastPaymentAmount = payment.Amount;

                    // Create a SubscriptionPayment record for transaction history
                    var subscriptionPayment = new SubscriptionPayment
                    {
                        StoreId = store.StoreID,
                        Amount = payment.Amount,
                        PaymentDate = DateTime.UtcNow,
                        Reference = $"Subscription renewal - {transferId}"
                    };
                    _context.SubscriptionPayments.Add(subscriptionPayment);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Payment of ${payment.Amount:F2} was successfully processed.";

                // 3. Redirect to ReturnUrl if provided and safe, else fallback
                if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl);

                // Fallback: if subscription payment, go to account statement; otherwise product creation
                if (isSubscriptionPayment)
                    return RedirectToPage("/StoreOwner/AccountStatement");

                return RedirectToPage("/StoreOwner/Products/Create");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Store owner payment failed for payment {PaymentId}.", PaymentId);
                ErrorMessage = "An error occurred while processing your payment. Please try again.";
                return Page();
            }
        }

        // -------------------------------------------------------------
        // Helper methods
        // -------------------------------------------------------------

        private async Task<StorePayment?> LoadPaymentForCurrentStoreOwnerAsync(int paymentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            int userIdInt = user.Id;

            var store = await _context.Stores
                .FirstOrDefaultAsync(s => s.OwnerUserID == userIdInt);
            if (store == null) return null;

            return await _context.StorePayments
                .Include(sp => sp.Store)
                .FirstOrDefaultAsync(sp => sp.StorePaymentId == paymentId && sp.StoreId == store.StoreID);
        }

        private void NormalizePaymentInput()
        {
            CardholderName = CardholderName?.Trim() ?? string.Empty;
            CardNumber = CardNumber?.Replace(" ", "").Replace("-", "").Trim() ?? string.Empty;
            ExpiryDate = ExpiryDate?.Trim() ?? string.Empty;
            Cvv = Cvv?.Trim() ?? string.Empty;
        }

        private string? ValidateCardInput()
        {
            if (string.IsNullOrWhiteSpace(CardholderName) || CardholderName.Length < 3)
                return "Cardholder name is required (min 3 characters).";
            if (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Length != 16 || !CardNumber.All(char.IsDigit))
                return "Card number must be exactly 16 digits.";
            if (!IsValidLuhn(CardNumber))
                return "Invalid card number. Please check the digits.";
            if (!IsValidExpiry(ExpiryDate))
                return "Invalid expiry date (MM/YY) or card has expired.";
            if (string.IsNullOrWhiteSpace(Cvv) || Cvv.Length != 3 || !Cvv.All(char.IsDigit))
                return "CVV must be exactly 3 digits.";
            return null;
        }

        private static bool IsValidLuhn(string cardNumber)
        {
            int sum = 0;
            bool doubleDigit = false;
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = cardNumber[i] - '0';
                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9) digit -= 9;
                }
                sum += digit;
                doubleDigit = !doubleDigit;
            }
            return sum % 10 == 0;
        }

        private static bool IsValidExpiry(string expiry)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(expiry, @"^\d{2}/\d{2}$")) return false;
            var parts = expiry.Split('/');
            if (!int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int year)) return false;
            if (month < 1 || month > 12) return false;
            int currentYear = DateTime.UtcNow.Year % 100;
            int currentMonth = DateTime.UtcNow.Month;
            return year > currentYear || (year == currentYear && month >= currentMonth);
        }

        // Stripe methods – using RequestOptions for connected account
        private async Task FetchStripeBalance(string stripeAccountId)
        {
            try
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var balanceService = new BalanceService();
                var balance = await balanceService.GetAsync(
                    options: null,
                    requestOptions: new RequestOptions { StripeAccount = stripeAccountId }
                );

                var available = balance.Available.FirstOrDefault();
                CurrentBalance = available?.Amount ?? 0;
                CurrentBalance /= 100; // Stripe amounts in cents
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch Stripe balance for account {AccountId}.", stripeAccountId);
                CurrentBalance = -1;
            }
        }

        private async Task<bool> CheckStripeBalance(string stripeAccountId, decimal requiredAmount)
        {
            try
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var balanceService = new BalanceService();
                var balance = await balanceService.GetAsync(
                    options: null,
                    requestOptions: new RequestOptions { StripeAccount = stripeAccountId }
                );

                long totalAvailableCents = balance.Available.Sum(b => b.Amount);
                decimal totalAvailable = totalAvailableCents / 100m;
                return totalAvailable >= requiredAmount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Stripe balance for account {AccountId}.", stripeAccountId);
                return false;
            }
        }
    }
}