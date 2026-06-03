using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerWishlistModel : PageModel
    {
        private readonly WishlistManager _wishlistManager;
        private readonly CartManager _cartManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public CustomerWishlistModel(
            WishlistManager wishlistManager,
            CartManager cartManager,
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _wishlistManager = wishlistManager;
            _cartManager = cartManager;
            _userManager = userManager;
            _context = context;
        }

        public List<WishlistDTO> WishlistItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            WishlistItems = await _wishlistManager
                .GetCustomerWishlistAsync(customerId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _wishlistManager.RemoveFromWishlistAsync(
                customerId.Value,
                productId);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMoveToCartAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _cartManager.AddToCartAsync(
                productId,
                1,
                customerId.Value,
                null);

            await _wishlistManager.RemoveFromWishlistAsync(
                customerId.Value,
                productId);

            return RedirectToPage();
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            return customer?.CustomerID;
        }
    }
}