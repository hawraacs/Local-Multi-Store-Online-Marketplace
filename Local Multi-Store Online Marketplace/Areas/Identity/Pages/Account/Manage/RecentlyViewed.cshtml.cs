using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Multi_Store.Pages.Customer
{
    public class RecentlyViewedModel : PageModel
    {
        private readonly RecentlyViewedManager _recentlyViewedManager;
        private readonly WishlistManager _wishlistManager;

        public RecentlyViewedModel(
            RecentlyViewedManager recentlyViewedManager,
            WishlistManager wishlistManager)
        {
            _recentlyViewedManager = recentlyViewedManager;
            _wishlistManager = wishlistManager;
        }

        public List<RecentlyViewedProductDTO> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            int customerId = GetCurrentCustomerId();
            Products = await _recentlyViewedManager.GetCustomerRecentlyViewedAsync(customerId);
        }

        public async Task<IActionResult> OnPostAddToWishlistAsync(int productId)
        {
            int customerId = GetCurrentCustomerId();
            await _wishlistManager.AddToWishlistAsync(customerId, productId);

            return RedirectToPage();
        }

        private int GetCurrentCustomerId()
        {
            return 1;
        }
    }
}