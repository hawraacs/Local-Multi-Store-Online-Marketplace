using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Entities;
    using Multi_Store.Infrastructure.Data;
    using Stripe;
    using System;
    using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{

    [Authorize(Roles = "StoreOwner")]
        public class StoreOwnerPaymentModel : PageModel
        {
            private readonly ApplicationDbContext _context;
            private readonly UserManager<User> _userManager;
            private readonly ILogger<StoreOwnerPaymentModel> _logger;
            private readonly StripeConfiguration _stripeConfig;

            public StoreOwnerPaymentModel(
                ApplicationDbContext context,
                UserManager<User> userManager,
                ILogger<StoreOwnerPaymentModel> logger,
                StripeConfiguration stripeConfig)
            {
                _context = context;
                _userManager = userManager;
                _logger = logger;
                _stripeConfig = stripeConfig;
            }

            public StorePayment? Payment { get; set; }
            public string? ErrorMessage { get; set; }
            public decimal CurrentBalance { get; set; }

            [BindProperty]
            public int PaymentId { get; set; }

            // For the form
            [BindProperty]
            public string CardholderName { get; set; } = string.Empty;

            [BindProperty]
            public string CardNumber { get; set; } = string.Empty;

            [BindProperty]
            public string ExpiryDate { get; set; } = string.Empty;

            [BindProperty]
            public string Cvv { get; set; } = string.Empty;

            public async Task<IActionResult> OnGetAsync(int paymentId)
            {
                PaymentId = paymentId;

                var payment = await LoadPaymentForCurrentStoreOwnerAsync(paymentId);
                if (payment == null)
                {
                    ErrorMessage = "Payment request not found or you are not authorized.";
                    return Page();
                }

                Payment = payment;

                // Check if already paid
                if (payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = "This payment has already been completed.";
                    return RedirectToPage("/StoreOwnerDashboard");
                }

                // Validate that the store has a Stripe account
                if (string.IsNullOrWhiteSpace(payment.Store.StripeAccountId))
                {
                    ErrorMessage = "Your store does not have a Stripe account linked. Please contact support.";
                    return Page();
                }

                // Optionally fetch current Stripe balance for display
                await FetchStripeBalance(payment.Store.StripeAccountId);

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

                // Re-validate state
                if (payment.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = "This payment is already completed.";
                    return RedirectToPage("/StoreOwnerDashboard");
                }

                if (string.IsNullOrWhiteSpace(payment.Store.StripeAccountId))
                {
                    ErrorMessage = "Your store does not have a Stripe account linked.";
                    return Page();
                }

                NormalizePaymentInput();

                // Validate card details (same logic as customer version)
                var cardValidationError = ValidateCardInput();
                if (!string.IsNullOrWhiteSpace(cardValidationError))
                {
                    ErrorMessage = cardValidationError;
                    return Page();
                }

                // Check Stripe balance
                var balanceSufficient = await CheckStripeBalance(payment.Store.StripeAccountId, payment.Amount);
                if (!balanceSufficient)
                {
                    ErrorMessage = "Insufficient funds in your Stripe account. Please add funds and try again.";
                    return Page();
                }

                // Process the payment (simulate or actually debit via Stripe)
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Optionally create a Stripe transfer (or just record)
                    // For demonstration, we just mark as paid and record a transfer ID placeholder.
                    var transferId = $"TRANSFER-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

                    payment.Status = "Paid";
                    payment.PaidAt = DateTime.UtcNow;
                    payment.StripeTransferId = transferId;

                    // Optionally create a Payment record (if you have a generic Payment table)
                    // You can reuse the existing Payment entity or create a separate StorePaymentTransaction.

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = $"Payment of ${payment.Amount:F2} was successfully processed. Reference: {transferId}.";
                    return RedirectToPage("/StoreOwnerDashboard");
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

                // Get the store owned by this user (assuming one store per user)
                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.UserId == user.Id);
                if (store == null) return null;

                return await _context.StorePayments
                    .Include(sp => sp.Store)
                    .FirstOrDefaultAsync(sp => sp.StorePaymentId == paymentId && sp.StoreId == store.StoreId);
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
                // Same validation logic as in OnlinePaymentModel – you can copy that.
                // I'll include a condensed version here.
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

            // Stripe integration methods
            private async Task FetchStripeBalance(string stripeAccountId)
            {
                try
                {
                    // Set your API key – loaded from configuration
                    StripeConfiguration.ApiKey = _stripeConfig.SecretKey;

                    var balanceService = new BalanceService();
                    var balance = await balanceService.GetAsync(new BalanceGetOptions
                    {
                        StripeAccount = stripeAccountId
                    });

                    // Get available balance (assuming currency is USD)
                    var available = balance.Available.FirstOrDefault();
                    CurrentBalance = available?.Amount ?? 0;
                    // Convert from cents to dollars if needed (Stripe amounts are in cents)
                    CurrentBalance /= 100; // Stripe returns in cents
                }
                catch
                {
                    // Log error, but don't block the page
                    CurrentBalance = -1; // indicate error
                }
            }

            private async Task<bool> CheckStripeBalance(string stripeAccountId, decimal requiredAmount)
            {
                try
                {
                    StripeConfiguration.ApiKey = _stripeConfig.SecretKey;

                    var balanceService = new BalanceService();
                    var balance = await balanceService.GetAsync(new BalanceGetOptions
                    {
                        StripeAccount = stripeAccountId
                    });

                    // Sum available balances (in cents)
                    long totalAvailableCents = balance.Available.Sum(b => b.Amount);
                    decimal totalAvailable = totalAvailableCents / 100m; // convert to dollars

                    return totalAvailable >= requiredAmount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check Stripe balance for account {AccountId}.", stripeAccountId);
                    return false; // fail safe
                }
            }
        }
    }