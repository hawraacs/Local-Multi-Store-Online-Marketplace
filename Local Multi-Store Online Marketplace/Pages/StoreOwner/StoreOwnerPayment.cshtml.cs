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

// Alias to avoid ambiguity with your Customer entity
using StripeCustomer = Stripe.Customer;
using StripePaymentMethod = Stripe.PaymentMethod;

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
        public decimal CurrentBalance { get; set; } = -1;

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
                return RedirectToLocalOrDashboard();
            }

            if (string.IsNullOrWhiteSpace(payment.Store.StripeAccountId))
            {
                CurrentBalance = 9999; // dummy for demo
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
                return RedirectToLocalOrDashboard();
            }

            NormalizePaymentInput();

            var cardValidationError = ValidateCardInput();
            if (!string.IsNullOrWhiteSpace(cardValidationError))
            {
                ErrorMessage = cardValidationError;
                return Page();
            }

            var store = payment.Store;

            // Save payment method if not already stored
            if (string.IsNullOrEmpty(store.StripeCustomerId) || string.IsNullOrEmpty(store.StripePaymentMethodId))
            {
                await SavePaymentMethodAsync(store, CardNumber, ExpiryDate, Cvv, CardholderName);
                await _context.SaveChangesAsync();
            }

            // Check balance (if Stripe account linked)
            bool balanceSufficient = true;
            if (!string.IsNullOrWhiteSpace(store.StripeAccountId))
            {
                balanceSufficient = await CheckStripeBalance(store.StripeAccountId, payment.Amount);
            }

            if (!balanceSufficient)
            {
                ErrorMessage = "Insufficient funds in your Stripe account. Please add funds or use a card payment.";
                return Page();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var transferId = $"TRANSFER-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

                payment.Status = "Paid";
                payment.PaidAt = DateTime.UtcNow;
                payment.StripeTransferId = transferId;

                bool isSubscriptionPayment = payment.Description?.Equals("Monthly Subscription Fee", StringComparison.OrdinalIgnoreCase) == true;

                if (isSubscriptionPayment)
                {
                    DateTime newExpiry = DateTime.UtcNow.AddMonths(1);
                    if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate.Value > DateTime.UtcNow)
                        newExpiry = store.SubscriptionExpiryDate.Value.AddMonths(1);

                    store.SubscriptionStatus = "Active";
                    store.SubscriptionExpiryDate = newExpiry;
                    store.LastPaymentDate = DateTime.UtcNow;
                    store.LastPaymentAmount = payment.Amount;

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
                return RedirectToLocalOrDashboard();
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
        // Stripe Payment Method Saving (uses aliases)
        // -------------------------------------------------------------
        private async Task SavePaymentMethodAsync(Store store, string cardNumber, string expiry, string cvv, string cardholderName)
        {
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            var expParts = expiry.Split('/');
            var expMonth = int.Parse(expParts[0]);
            var expYear = int.Parse(expParts[1]);

            var paymentMethodOptions = new PaymentMethodCreateOptions
            {
                Type = "card",
                Card = new PaymentMethodCardOptions
                {
                    Number = cardNumber,
                    ExpMonth = expMonth,
                    ExpYear = expYear,
                    Cvc = cvv,
                },
                BillingDetails = new PaymentMethodBillingDetailsOptions
                {
                    Name = cardholderName,
                }
            };
            var paymentMethodService = new PaymentMethodService();
            var paymentMethod = await paymentMethodService.CreateAsync(paymentMethodOptions);

            var customerService = new CustomerService();
            StripeCustomer customer;

            if (!string.IsNullOrEmpty(store.StripeCustomerId))
            {
                customer = await customerService.GetAsync(store.StripeCustomerId);
            }
            else
            {
                var customerOptions = new CustomerCreateOptions
                {
                    Email = store.Email,
                    Name = store.StoreName,
                    PaymentMethod = paymentMethod.Id,
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = paymentMethod.Id,
                    },
                };
                customer = await customerService.CreateAsync(customerOptions);
                store.StripeCustomerId = customer.Id;
            }

            // Attach payment method to customer
            var attachOptions = new PaymentMethodAttachOptions
            {
                Customer = customer.Id,
            };
            await paymentMethodService.AttachAsync(paymentMethod.Id, attachOptions);

            // Set as default
            var updateOptions = new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    DefaultPaymentMethod = paymentMethod.Id,
                }
            };
            await customerService.UpdateAsync(customer.Id, updateOptions);

            store.StripePaymentMethodId = paymentMethod.Id;
        }

        // -------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------
        private IActionResult RedirectToLocalOrDashboard()
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return Redirect(ReturnUrl);

            if (Payment?.Description?.Equals("Monthly Subscription Fee", StringComparison.OrdinalIgnoreCase) == true)
                return RedirectToPage("/StoreOwner/AccountStatement");

            return RedirectToPage("/StoreOwner/Products/Create");
        }

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
                CurrentBalance /= 100;
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