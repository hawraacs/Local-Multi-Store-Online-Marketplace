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

        public CustomerPromotionsModel(IPromotionManager promotionManager)
        {
            _promotionManager = promotionManager;
        }

        public List<PromotionRecipient> Promotions { get; set; } = new();

        public async Task OnGetAsync()
        {
            int userId = GetCurrentUserId();
            Promotions = await _promotionManager.GetCustomerPromotionsAsync(userId);
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(int promotionRecipientId)
        {
            int userId = GetCurrentUserId();

            await _promotionManager.MarkAsReadAsync(promotionRecipientId, userId);

            return RedirectToPage();
        }

        private int GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdValue))
                throw new InvalidOperationException("User is not logged in.");

            return int.Parse(userIdValue);
        }
    }
}