using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using System.Security.Claims;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerPromotionsModel : PageModel
    {
        private readonly IPromotionManager _promotionManager;
        private readonly ILogger<CustomerPromotionsModel> _logger;

        public CustomerPromotionsModel(IPromotionManager promotionManager, ILogger<CustomerPromotionsModel> logger)
        {
            _promotionManager = promotionManager;
            _logger = logger;
        }

        public List<PromotionRecipient> Promotions { get; set; } = new();

        public async Task OnGetAsync()
        {
            int userId = GetCurrentUserId();
            var promotions = await _promotionManager.GetCustomerPromotionsAsync(userId);

            // Unread first, then most recent — keeps the page scannable as the list grows.
            Promotions = promotions
                .OrderBy(p => p.IsRead)
                .ThenByDescending(p => p.CreatedAt)
                .ToList();
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(int promotionRecipientId)
        {
            int userId = GetCurrentUserId();

            try
            {
                await _promotionManager.MarkAsReadAsync(promotionRecipientId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark promotion {PromotionRecipientId} as read for user {UserId}",
                    promotionRecipientId, userId);

                if (IsAjaxRequest())
                    return new JsonResult(new { success = false }) { StatusCode = 400 };

                return RedirectToPage();
            }

            if (IsAjaxRequest())
                return new JsonResult(new { success = true, promotionRecipientId });

            return RedirectToPage();
        }

        private bool IsAjaxRequest() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private int GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdValue))
                throw new InvalidOperationException("User is not logged in.");

            return int.Parse(userIdValue);
        }
    }
}
