using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Customer")]
    public class CustomerWishlistModel : PageModel
    {
        private readonly WishlistManager _wishlistManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public CustomerWishlistModel(
            WishlistManager wishlistManager,
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _wishlistManager = wishlistManager;
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

            TempData["Success"] = "Product removed from wishlist.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMoveToCartAsync(int productId, int quantity = 1)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive &&
                    p.Quantity > 0);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            if (quantity > product.Quantity)
            {
                quantity = product.Quantity;
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId.Value);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerID = customerId.Value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci =>
                    ci.CartID == cart.CartID &&
                    ci.ProductID == productId);

            if (existingItem != null)
            {
                // Product is already in the cart — increase its
                // quantity by the amount chosen on the wishlist page,
                // capped at available stock, instead of creating a
                // duplicate row or silently dropping the request.
                existingItem.Quantity = Math.Min(
                    existingItem.Quantity + quantity,
                    product.Quantity);

                cart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await _wishlistManager.RemoveFromWishlistAsync(
                    customerId.Value,
                    productId);

                TempData["Success"] = $"{product.ProductName} quantity updated in your cart.";

                return RedirectToPage();
            }

            var cartItem = new CartItem
            {
                CartID = cart.CartID,
                ProductID = productId,
                Quantity = quantity,
                PriceAtAddTime = product.Price,
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _wishlistManager.RemoveFromWishlistAsync(
                customerId.Value,
                productId);

            TempData["Success"] = $"{product.ProductName} moved to your cart.";

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