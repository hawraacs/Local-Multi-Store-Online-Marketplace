using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.DTOs;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using System.Security.Claims;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Promotions
{
    [Authorize(Roles = "StoreOwner")]
    public class CreateModel : PageModel
    {
        private readonly IPromotionManager _promotionManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IPromotionManager promotionManager,
            ApplicationDbContext context,
            ILogger<CreateModel> logger)
        {
            _promotionManager = promotionManager;
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public PromotionDTO Promotion { get; set; } = new();

        // Products belonging to this store owner's store, for the "select a
        // product" dropdown used by the no-coupon Automatic Sale path only.
        public List<ProductOption> MyProducts { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Promotion = new PromotionDTO
            {
                AudienceType = "AllCustomers",

                // BUGFIX: was DateTime.Today (local server time) — every other date
                // calculation in this app uses DateTime.UtcNow. On a server not
                // running in UTC, "today" here could silently disagree by up to a
                // day with "today" as understood everywhere else (including the
                // "cannot be in the past" check below, which had the same issue).
                CouponEndDate = DateTime.UtcNow.Date.AddDays(7)
            };

            await LoadMyProductsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Promotion == null)
            {
                ErrorMessage =
                    "Promotion information is missing.";

                await LoadMyProductsAsync();
                return Page();
            }

            // Normalize the coupon code before saving.
            if (!string.IsNullOrWhiteSpace(
                    Promotion.CouponCode))
            {
                Promotion.CouponCode =
                    Promotion.CouponCode
                        .Trim()
                        .ToUpperInvariant();
            }

            // Validate coupon fields only when CreateCoupon is checked.
            if (Promotion.CreateCoupon)
            {
                if (string.IsNullOrWhiteSpace(
                        Promotion.CouponCode))
                {
                    ModelState.AddModelError(
                        "Promotion.CouponCode",
                        "Coupon code is required when creating a coupon.");
                }

                if (Promotion.DiscountValue <= 0)
                {
                    ModelState.AddModelError(
                        "Promotion.DiscountValue",
                        "Discount value must be greater than zero.");
                }

                if (string.Equals(
                        Promotion.DiscountType,
                        "Percentage",
                        StringComparison.OrdinalIgnoreCase) &&
                    Promotion.DiscountValue > 100)
                {
                    ModelState.AddModelError(
                        "Promotion.DiscountValue",
                        "Percentage discount cannot exceed 100%.");
                }

                // BUGFIX: none of these three optional fields were ever checked for
                // sane values — a negative minimum order, a negative or zero max
                // discount, or a negative usage limit could all be submitted with
                // no server-side pushback.
                if (Promotion.MinimumOrderAmount.HasValue &&
                    Promotion.MinimumOrderAmount.Value < 0)
                {
                    ModelState.AddModelError(
                        "Promotion.MinimumOrderAmount",
                        "Minimum order amount cannot be negative.");
                }

                if (Promotion.MaximumDiscountAmount.HasValue &&
                    Promotion.MaximumDiscountAmount.Value <= 0)
                {
                    ModelState.AddModelError(
                        "Promotion.MaximumDiscountAmount",
                        "Maximum discount amount must be greater than zero.");
                }

                if (Promotion.UsageLimit.HasValue &&
                    Promotion.UsageLimit.Value <= 0)
                {
                    ModelState.AddModelError(
                        "Promotion.UsageLimit",
                        "Usage limit must be greater than zero.");
                }

                // CouponEndDate is nullable DateTime?.
                if (!Promotion.CouponEndDate.HasValue)
                {
                    ModelState.AddModelError(
                        "Promotion.CouponEndDate",
                        "Coupon end date is required when creating a coupon.");
                }
                else if (Promotion.CouponEndDate.Value.Date <
                         DateTime.UtcNow.Date)
                {
                    ModelState.AddModelError(
                        "Promotion.CouponEndDate",
                        "Coupon end date cannot be in the past.");
                }
            }
            else
            {
                // BUGFIX: if a store owner checks "Create a coupon", fills in some
                // fields, then unchecks it before submitting, those values were
                // previously still bound on the DTO and passed straight through to
                // SendPromotionAsync — CreateCoupon being false didn't actually
                // guarantee no coupon data went along with it. Cleared explicitly
                // here so the DTO unambiguously means "no coupon" downstream,
                // regardless of what IPromotionManager does or doesn't check itself.
                Promotion.CouponCode = null;
                Promotion.MinimumOrderAmount = null;
                Promotion.MaximumDiscountAmount = null;
                Promotion.UsageLimit = null;

                if (Promotion.ProductID.HasValue)
                {
                    // Automatic sale path — same validation rigor as the
                    // coupon path above. CouponEndDate is intentionally
                    // NOT cleared here: it's reused as the sale's
                    // (optional) end date.
                    if (Promotion.DiscountValue <= 0)
                    {
                        ModelState.AddModelError(
                            "Promotion.DiscountValue",
                            "Discount value must be greater than zero.");
                    }

                    if (string.Equals(
                            Promotion.DiscountType,
                            "Percentage",
                            StringComparison.OrdinalIgnoreCase) &&
                        Promotion.DiscountValue > 100)
                    {
                        ModelState.AddModelError(
                            "Promotion.DiscountValue",
                            "Percentage discount cannot exceed 100%.");
                    }

                    if (Promotion.CouponEndDate.HasValue &&
                        Promotion.CouponEndDate.Value.Date <
                            DateTime.UtcNow.Date)
                    {
                        ModelState.AddModelError(
                            "Promotion.CouponEndDate",
                            "Sale end date cannot be in the past.");
                    }
                }
                else
                {
                    Promotion.CouponEndDate = null;
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadMyProductsAsync();
                return Page();
            }

            try
            {
                var userId =
                    GetCurrentUserId();

                var recipientCount =
                    await _promotionManager
                        .SendPromotionAsync(
                            Promotion,
                            userId);

                TempData["SuccessMessage"] =
                    $"Promotion sent successfully to " +
                    $"{recipientCount} customers.";

                return RedirectToPage(
                    "/StoreOwner/Promotions/Index");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error sending promotion for store owner user {UserId}.",
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier));

                // BUGFIX: previously showed exception.Message directly to the user —
                // that can leak internal details (stack/provider-specific error text)
                // and usually isn't meaningful to a store owner anyway. The full
                // exception is still logged above for diagnosis.
                ErrorMessage =
                    "Something went wrong while sending this promotion. Please try again.";

                await LoadMyProductsAsync();
                return Page();
            }
        }

        private async Task LoadMyProductsAsync()
        {
            var userId = GetCurrentUserId();

            var storeId = await _context.Stores
                .Where(s => s.OwnerUserID == userId)
                .Select(s => (int?)s.StoreID)
                .FirstOrDefaultAsync();

            if (storeId == null)
            {
                MyProducts = new List<ProductOption>();
                return;
            }

            MyProducts = await _context.Products
                .Where(p => p.StoreID == storeId.Value && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new ProductOption
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Price = p.Price
                })
                .ToListAsync();
        }

        private int GetCurrentUserId()
        {
            var userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(
                    userIdValue))
            {
                throw new InvalidOperationException(
                    "User is not logged in.");
            }

            if (!int.TryParse(
                    userIdValue,
                    out var userId))
            {
                throw new InvalidOperationException(
                    "The current user ID is invalid.");
            }

            return userId;
        }
    }

    public class ProductOption
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }
}