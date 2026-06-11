using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using System.Security.Claims;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Promotions
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly IPromotionManager _promotionManager;

        public IndexModel(IPromotionManager promotionManager)
        {
            _promotionManager = promotionManager;
        }

        public List<Promotion> Promotions { get; set; } = new();

        public async Task OnGetAsync()
        {
            int userId = GetCurrentUserId();
            Promotions = await _promotionManager.GetMyStorePromotionsAsync(userId);
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