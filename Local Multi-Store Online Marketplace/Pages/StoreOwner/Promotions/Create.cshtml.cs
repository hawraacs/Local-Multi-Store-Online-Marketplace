using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.DTOs;
using Multi_Store.Core.Interfaces;
using System.Security.Claims;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Promotions
{
    [Authorize(Roles = "StoreOwner")]
    public class CreateModel : PageModel
    {
        private readonly IPromotionManager _promotionManager;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IPromotionManager promotionManager,
            ILogger<CreateModel> logger)
        {
            _promotionManager = promotionManager;
            _logger = logger;
        }

        [BindProperty]
        public PromotionDTO Promotion { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public void OnGet()
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
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Promotion == null)
            {
                ErrorMessage =
                    "Promotion information is missing.";

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

            // A Promotion now always requires a coupon — the no-coupon
            // "Automatic Sale" path has been removed. Checked up front and
            // returns immediately, same pattern as the "Promotion == null"
            // guard above, since this page has no validation-summary
            // element for a keyless ModelState error to render into —
            // Model.ErrorMessage is the one place on this page proven to
            // actually display to the store owner.
            if (!Promotion.CreateCoupon)
            {
                ErrorMessage =
                    "A coupon is required to send a promotion.";

                return Page();
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
                Promotion.CouponEndDate = null;
            }

            if (!ModelState.IsValid)
            {
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

                return Page();
            }
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
}
