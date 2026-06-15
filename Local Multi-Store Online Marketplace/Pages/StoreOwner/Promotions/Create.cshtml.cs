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

        public CreateModel(IPromotionManager promotionManager)
        {
            _promotionManager = promotionManager;
        }

        [BindProperty]
        public PromotionDTO Promotion { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            Promotion = new PromotionDTO
            {
                AudienceType = "AllCustomers"
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            try
            {
                int userId = GetCurrentUserId();

                int count = await _promotionManager.SendPromotionAsync(Promotion, userId);

                TempData["SuccessMessage"] = $"Promotion sent successfully to {count} customers.";

                return RedirectToPage("/StoreOwner/Promotions/Index");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
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