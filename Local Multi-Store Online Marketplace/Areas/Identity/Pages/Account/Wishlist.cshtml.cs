using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Multi_Store.Pages.Customer
{
    public class WishlistModel : PageModel
    {
        private readonly WishlistManager _wishlistManager;
        private readonly CartManager _cartManager;

        public WishlistModel(
            WishlistManager wishlistManager,
            CartManager cartManager)
        {
            _wishlistManager = wishlistManager;
            _cartManager = cartManager;
        }

        public List<WishlistDTO> WishlistItems { get; set; } = new();

        public async Task OnGetAsync()
        {
            int customerId = GetCurrentCustomerId();
            WishlistItems = await _wishlistManager.GetCustomerWishlistAsync(customerId);
        }

        public async Task<IActionResult> OnPostRemoveAsync(int productId)
        {
            int customerId = GetCurrentCustomerId();
            await _wishlistManager.RemoveFromWishlistAsync(customerId, productId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMoveToCartAsync(int productId)
        {
            int customerId = GetCurrentCustomerId();

            await _cartManager.AddToCartAsync(
                productId,
                1,
                customerId,
                null);

            await _wishlistManager.RemoveFromWishlistAsync(
                customerId,
                productId);

            return RedirectToPage();
        }

        private int GetCurrentCustomerId()
        {
            return 1;
        }
    }
}