using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
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
        private readonly NotificationManager _notifications;   // ADD THIS

        public StoreOwnerPaymentModel(
    ApplicationDbContext context,
    UserManager<User> userManager,
    ILogger<StoreOwnerPaymentModel> logger,
    IConfiguration configuration,
    NotificationManager notifications)                  // ADD THIS
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
            _notifications = notifications;                     // ADD THIS
        }

        public StorePayment? Payment { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal CurrentBalance { get; set; } = -1;

        // Sent to the browser so Stripe.js can tokenize the card client-side.
        // Safe to expose publicly.
        public string? StripePublishableKey { get; set; }

        // Client secret for the SetupIntent used to verify the card with the
        // issuer via Stripe.js. Single-use and scoped to this SetupIntent only.
        public string? ClientSecret { get; set; }

        [BindProperty]
        public int PaymentId { get; set; }

        [BindProperty]
        public string CardholderName { get; set; } = string.Empty;

        // The ONLY card-related value that ever reaches our server. It is a
        // reference to a Stripe SetupIntent that Stripe.js already confirmed
        // in the browser — the raw card number never touches this server.
        [BindProperty]
        public string SetupIntentId { get; set; } = string.Empty;

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

            var store = payment.Store;

            if (string.IsNullOrWhiteSpace(store.StripeAccountId))
            {
                CurrentBalance = 9999; // dummy for demo
            }
            else
            {
                await FetchStripeBalance(store.StripeAccountId);
            }

            // Prepare Stripe.js: a customer + SetupIntent so the browser can
            // verify the card directly with Stripe/the issuing bank without
            // ever sending the card number to this server.
            await PrepareStripeSetupAsync(store);

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

            var store = payment.Store;

            CardholderName = CardholderName?.Trim() ?? string.Empty;
            SetupIntentId = SetupIntentId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(CardholderName) || CardholderName.Length < 3)
            {
                ErrorMessage = "Cardholder name is required (min 3 characters).";
                await PrepareStripeSetupAsync(store);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(SetupIntentId))
            {
                ErrorMessage = "Card verification is required before continuing.";
                await PrepareStripeSetupAsync(store);
                return Page();
            }

            // Confirm — server-side — that Stripe actually verified this card
            // with the issuer. Never trust the client's word alone.
            var attachResult = await ConfirmAndAttachPaymentMethodAsync(store, SetupIntentId);
            if (!attachResult.Success)
            {
                ErrorMessage = attachResult.ErrorMessage ?? "Your card could not be verified. Please try again.";
                await PrepareStripeSetupAsync(store);
                return Page();
            }

            await _context.SaveChangesAsync();

            // Check balance (if Stripe account linked)
            bool balanceSufficient = true;
            if (!string.IsNullOrWhiteSpace(store.StripeAccountId))
            {
                balanceSufficient = await CheckStripeBalance(store.StripeAccountId, payment.Amount);
            }

            if (!balanceSufficient)
            {
                ErrorMessage = "Insufficient funds in your Stripe account. Please add funds or use a card payment.";
                await PrepareStripeSetupAsync(store);
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
                // NEW — notify admins that a store payment came in
                await _notifications.SendToAllAdminsAsync(
                    title: "Store Payment Received",
                    message: $"{store.StoreName} paid ${payment.Amount:F2}" +
                             (isSubscriptionPayment ? " (Monthly Subscription Fee)" : $" — {payment.Description}"),
                    type: "Payment",
                    referenceId: payment.StorePaymentId
                );

                TempData["Success"] = $"Payment of ${payment.Amount:F2} was successfully processed.";
                return RedirectToLocalOrDashboard();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Store owner payment failed for payment {PaymentId}.", PaymentId);
                ErrorMessage = "An error occurred while processing your payment. Please try again.";
                await PrepareStripeSetupAsync(store);
                return Page();
            }
        }

        // -------------------------------------------------------------
        // Stripe.js bootstrap: create/reuse a Customer and issue a fresh
        // SetupIntent so the browser can verify a card without ever
        // exposing the raw PAN to our server.
        // -------------------------------------------------------------
        private async Task PrepareStripeSetupAsync(Store store)
        {
            try
            {
                StripePublishableKey = _configuration["Stripe:PublishableKey"];
                if (string.IsNullOrWhiteSpace(StripePublishableKey))
                {
                    _logger.LogWarning("Stripe:PublishableKey is not configured.");
                    return;
                }

                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var customer = await GetOrCreateCustomerAsync(store);

                var setupIntentService = new SetupIntentService();
                var setupIntent = await setupIntentService.CreateAsync(new SetupIntentCreateOptions
                {
                    Customer = customer.Id,
                    PaymentMethodTypes = new System.Collections.Generic.List<string> { "card" },
                    Usage = "off_session"
                });

                ClientSecret = setupIntent.ClientSecret;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Failed to prepare Stripe SetupIntent for store {StoreId}.", store.StoreID);
                ClientSecret = null;
                // Surfacing the actual Stripe error (not just a generic message) makes this
                // debuggable during setup. Stripe's own error messages don't leak secrets.
                ErrorMessage = $"Card verification is temporarily unavailable ({ex.StripeError?.Message ?? ex.Message}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error preparing Stripe SetupIntent for store {StoreId}.", store.StoreID);
                ClientSecret = null;
                ErrorMessage = $"Card verification is temporarily unavailable ({ex.Message}).";
            }
        }

        private async Task<StripeCustomer> GetOrCreateCustomerAsync(Store store)
        {
            var customerService = new CustomerService();

            if (!string.IsNullOrWhiteSpace(store.StripeCustomerId))
            {
                try
                {
                    return await customerService.GetAsync(store.StripeCustomerId);
                }
                catch (StripeException)
                {
                    // Fall through and create a new customer if the stored ID is no longer valid.
                }
            }

            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = store.Email,
                Name = store.StoreName
            });

            store.StripeCustomerId = customer.Id;
            await _context.SaveChangesAsync();

            return customer;
        }

        // -------------------------------------------------------------
        // Verifies — server-side — that the SetupIntent the client claims
        // to have completed really did succeed with Stripe, then attaches
        // the resulting PaymentMethod (a token) as the customer's default.
        // At no point does this server see or store the card number.
        // -------------------------------------------------------------
        private async Task<(bool Success, string? ErrorMessage)> ConfirmAndAttachPaymentMethodAsync(Store store, string setupIntentId)
        {
            try
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var setupIntentService = new SetupIntentService();
                var setupIntent = await setupIntentService.GetAsync(setupIntentId);

                if (setupIntent.Status != "succeeded")
                {
                    return (false, "Card verification was not completed. Please try again.");
                }

                if (string.IsNullOrWhiteSpace(setupIntent.PaymentMethodId))
                {
                    return (false, "No verified card was found. Please try again.");
                }

                var customer = await GetOrCreateCustomerAsync(store);

                // Defense in depth: confirm the SetupIntent actually belongs
                // to this store's customer, not one supplied by a tampered request.
                if (!string.Equals(setupIntent.CustomerId, customer.Id, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "SetupIntent {SetupIntentId} customer mismatch for store {StoreId}.",
                        setupIntentId, store.StoreID);
                    return (false, "Card verification could not be matched to your account. Please try again.");
                }

                var customerService = new CustomerService();
                await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = setupIntent.PaymentMethodId
                    }
                });

                store.StripePaymentMethodId = setupIntent.PaymentMethodId;

                return (true, null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error confirming SetupIntent {SetupIntentId} for store {StoreId}.", setupIntentId, store.StoreID);
                return (false, ex.StripeError?.Message ?? "Your card could not be verified.");
            }
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