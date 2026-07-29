using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Promotions
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly IPromotionManager _promotionManager;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IPromotionManager promotionManager, ILogger<IndexModel> logger)
        {
            _promotionManager = promotionManager;
            _logger = logger;
        }

        public List<Promotion> Promotions { get; set; } = new();

        // Surfaced in the view if loading promotions fails — previously any
        // failure here (a bad claim, or GetMyStorePromotionsAsync throwing)
        // resulted in an unhandled exception page with no explanation.
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                int userId = GetCurrentUserId();
                Promotions = await _promotionManager.GetMyStorePromotionsAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading promotions for the current store owner.");
                ErrorMessage = "Something went wrong while loading your promotions. Please try again.";
                Promotions = new List<Promotion>();
            }
        }
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                await _promotionManager.DeletePromotionAsync(id);

                TempData["Success"] = "Promotion deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting promotion {PromotionId}", id);
                TempData["Error"] = "Unable to delete promotion.";
            }

            return RedirectToPage();
        }
        private int GetCurrentUserId()
        {
            // BUGFIX: previously used a bare int.Parse(userIdValue), which throws
            // FormatException/ArgumentNullException straight into an unhandled
            // exception page if the claim is missing or malformed. CreateModel.cs
            // already has a safe version of this exact same helper (TryParse +
            // a friendly exception) — mirrored here so the two don't drift apart.
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdValue))
                throw new InvalidOperationException("User is not logged in.");

            if (!int.TryParse(userIdValue, out var userId))
                throw new InvalidOperationException("The current user ID is invalid.");

            return userId;
        }
    }
}