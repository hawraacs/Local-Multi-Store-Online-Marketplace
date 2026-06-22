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

                // Safe default: coupon expires after 7 days.
                CouponEndDate = DateTime.Today.AddDays(7)
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

                // CouponEndDate is nullable DateTime?.
                if (!Promotion.CouponEndDate.HasValue)
                {
                    ModelState.AddModelError(
                        "Promotion.CouponEndDate",
                        "Coupon end date is required when creating a coupon.");
                }
                else if (Promotion.CouponEndDate.Value.Date <
                         DateTime.Today)
                {
                    ModelState.AddModelError(
                        "Promotion.CouponEndDate",
                        "Coupon end date cannot be in the past.");
                }
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

                ErrorMessage =
                    exception.Message;

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

