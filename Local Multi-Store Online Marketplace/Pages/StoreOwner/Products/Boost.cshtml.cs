using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using Stripe;
using Product = Multi_Store.Core.Entities.Product;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class BoostModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly StoreManager _storeManager;
        private readonly BoostManager _boostManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BoostModel> _logger;

        public BoostModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            StoreManager storeManager,
            BoostManager boostManager,
            IConfiguration configuration,
            ILogger<BoostModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _storeManager = storeManager;
            _boostManager = boostManager;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;
        public Store Store { get; set; } = null!;
        public ProductBoost? ExistingBoost { get; set; }
        public List<BoostPricingOption> PricingOptions => BoostManager.PricingOptions;

        [BindProperty]
        public int SelectedDurationDays { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToPage("/Login");

                var store = await _storeManager.GetByUserIdAsync(user.Id);
                if (store == null) return RedirectToPage("/StoreOwner/Dashboard");

                // Includes Images so the product thumbnail actually renders instead
                // of always falling back to the placeholder image.
                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == ProductId && p.StoreID == store.StoreID);

                if (product == null) return RedirectToPage("/StoreOwner/Home");

                Store = store;
                Product = product;
                ExistingBoost = await _boostManager.GetCurrentBoostForOwnerAsync(store.StoreID, ProductId);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Boost page for product {ProductId}.", ProductId);
                TempData["Error"] = "Something went wrong while loading this page. Please try again.";
                return RedirectToPage("/StoreOwner/Home");
            }
        }

        public async Task<IActionResult> OnPostStartBoostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            var store = await _storeManager.GetByUserIdAsync(user.Id);
            if (store == null) return RedirectToPage("/StoreOwner/Dashboard");

            // Same Include as OnGetAsync, for consistency.
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.ProductID == ProductId && p.StoreID == store.StoreID);
            if (product == null) return RedirectToPage("/StoreOwner/Home");

            // BUGFIX: previously nothing stopped this handler from creating a second
            // ProductBoost (and a second charge) for a product that already has an
            // active or pending one — the GET page only *hides* the form in that
            // case, which is UI-only and doesn't stop a duplicate/second POST (a
            // second tab, a resubmitted form, etc.). Re-checked here server-side.
            var currentBoost = await _boostManager.GetCurrentBoostForOwnerAsync(store.StoreID, product.ProductID);
            if (currentBoost != null)
            {
                TempData["Error"] = "This product already has an active or pending boost.";
                return RedirectToPage(new { ProductId });
            }

            var option = BoostManager.GetOption(SelectedDurationDays);
            if (option == null)
            {
                TempData["Error"] = "Please choose a valid boost duration.";
                return RedirectToPage(new { ProductId });
            }

            var boost = await _boostManager.CreateBoostRequestAsync(store.StoreID, product.ProductID, SelectedDurationDays);

            // Try saved card first, same pattern as subscription renewal
            if (!string.IsNullOrEmpty(store.StripeCustomerId) && !string.IsNullOrEmpty(store.StripePaymentMethodId))
            {
                var result = await ChargeSavedCardAsync(store, boost, option.Price);

                switch (result)
                {
                    case ChargeResult.Succeeded:
                        TempData["Success"] = $"'{product.ProductName}' is now boosted for {SelectedDurationDays} days.";
                        return RedirectToPage(new { ProductId });

                    case ChargeResult.ErrorAfterCharge:
                        // BUGFIX: this case did not exist before — a failure here used
                        // to be indistinguishable from a simple decline, which would
                        // fall through below and create a SECOND pending payment for
                        // something the store owner may have already been charged for.
                        // The card was charged; we must not ask them to pay again.
                        TempData["Error"] =
                            $"Your payment went through, but we couldn't finish activating the boost. " +
                            $"Please contact support and reference boost #{boost.ProductBoostID} — you will not be charged again.";
                        return RedirectToPage(new { ProductId });

                    case ChargeResult.Declined:
                        // fall through to manual payment
                        break;
                }
            }

            var pendingPayment = new StorePayment
            {
                StoreId = store.StoreID,
                Amount = option.Price,
                Description = $"Product Boost - {product.ProductName} ({SelectedDurationDays} days)",
                DueDate = DateTime.UtcNow.AddDays(3),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.StorePayments.Add(pendingPayment);
            await _context.SaveChangesAsync();

            boost.StorePaymentId = pendingPayment.StorePaymentId;
            await _context.SaveChangesAsync();

            return RedirectToPage("/StoreOwner/StoreOwnerPayment", new
            {
                paymentId = pendingPayment.StorePaymentId,
                returnUrl = Url.Page("/StoreOwner/Products/Boost", new { ProductId }),
                boostId = boost.ProductBoostID
            });
        }

        private enum ChargeResult
        {
            Succeeded,
            Declined,
            // BUGFIX: this outcome — Stripe charge succeeded, but our own
            // activation step afterward failed — previously wasn't represented at
            // all. It's now handled as its own case rather than being lumped in
            // with "declined", specifically so the caller never treats "already
            // charged" the same as "not charged yet".
            ErrorAfterCharge
        }

        private async Task<ChargeResult> ChargeSavedCardAsync(Store store, ProductBoost boost, decimal amount)
        {
            PaymentIntent intent;

            try
            {
                StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    Customer = store.StripeCustomerId,
                    PaymentMethod = store.StripePaymentMethodId,
                    OffSession = true,
                    Confirm = true,
                };

                var service = new PaymentIntentService();
                intent = await service.CreateAsync(options);

                if (intent.Status != "succeeded")
                {
                    return ChargeResult.Declined;
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Boost charge failed for boost {BoostId}.", boost.ProductBoostID);
                return ChargeResult.Declined;
            }

            // BUGFIX: everything past this point runs AFTER Stripe has already
            // confirmed the charge succeeded. Previously, any exception here
            // (e.g. a DB failure in ActivateBoostAsync) wasn't caught at all, since
            // the surrounding catch only handled StripeException — it would
            // propagate as an unhandled exception with the customer's card already
            // charged. Now caught separately and logged at Critical severity,
            // since "charged but not fulfilled" needs a person to notice and
            // reconcile it, not just a retry.
            try
            {
                boost.StripePaymentIntentId = intent.Id;
                await _boostManager.ActivateBoostAsync(boost.ProductBoostID);
                return ChargeResult.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Boost {BoostId} was charged via PaymentIntent {PaymentIntentId} but activation failed afterward. Needs manual reconciliation.",
                    boost.ProductBoostID,
                    intent.Id);
                return ChargeResult.ErrorAfterCharge;
            }
        }
    }
}
