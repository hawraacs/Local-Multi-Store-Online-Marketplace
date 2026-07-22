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
                var charged = await ChargeSavedCardAsync(store, boost, option.Price);
                if (charged)
                {
                    TempData["Success"] = $"'{product.ProductName}' is now boosted for {SelectedDurationDays} days.";
                    return RedirectToPage(new { ProductId });
                }
                // fall through to manual payment on failure
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

        private async Task<bool> ChargeSavedCardAsync(Store store, ProductBoost boost, decimal amount)
        {
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
                var intent = await service.CreateAsync(options);

                if (intent.Status == "succeeded")
                {
                    boost.StripePaymentIntentId = intent.Id;
                    await _boostManager.ActivateBoostAsync(boost.ProductBoostID);
                    return true;
                }
                return false;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Boost charge failed for boost {BoostId}.", boost.ProductBoostID);
                return false;
            }
        }
    }
}