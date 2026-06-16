using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class Customer1Model : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly WishlistManager _wishlistManager;
        private readonly MessagingManager _messagingManager;

        public Customer1Model(
            ApplicationDbContext context,
            UserManager<User> userManager,
            WishlistManager wishlistManager, MessagingManager messagingManager)
        {
            _context = context;
            _userManager = userManager;
            _wishlistManager = wishlistManager;
            _messagingManager = messagingManager;
        }

        public List<int> FollowingStoreIds { get; set; } = new();
        public List<Product> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadProductsAsync();
        }

        // =========================
        // ADD TO CART
        // =========================
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product not available.";
                return RedirectToPage();
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

            var exists = await _context.CartItems
                .AnyAsync(ci => ci.CartID == cart.CartID && ci.ProductID == productId);

            if (!exists)
            {
                _context.CartItems.Add(new CartItem
                {
                    CartID = cart.CartID,
                    ProductID = productId,
                    Quantity = 1,
                    PriceAtAddTime = product.Price,
                    AddedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Added to cart!";
            }

            return RedirectToPage();
        }

        // =========================
        // WISHLIST
        // =========================
        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product not available.";
                return RedirectToPage();
            }

            var exists = await _wishlistManager.IsInWishlistAsync(customerId.Value, productId);

            if (!exists)
            {
                await _wishlistManager.AddToWishlistAsync(customerId.Value, productId);
                TempData["Success"] = "Added to wishlist!";
            }

            return RedirectToPage();
        }

        // =========================
        // FOLLOW STORE
        // =========================
        public async Task<IActionResult> OnPostFollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null) return RedirectToPage();

            bool exists = await _context.StoreFollows
                .AnyAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

            if (!exists)
            {
                _context.StoreFollows.Add(new StoreFollow
                {
                    CustomerID = customerId.Value,
                    StoreID = storeId,
                    FollowedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnfollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null) return RedirectToPage();

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(f => f.CustomerID == customerId && f.StoreID == storeId);

            if (follow != null)
            {
                _context.StoreFollows.Remove(follow);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // =========================
        // SHARE BUTTON (NEW)
        // =========================
        public async Task<IActionResult> OnGetShareToStoreAsync(
     int productId,
     int storeOwnerId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage();

            await _messagingManager.SendProductAsync(
                user.Id,
                storeOwnerId,
                productId);
            TempData["Success"] = "Product shared successfully.";
            return RedirectToPage();
        }

        // =========================
        // LOAD FEED
        // =========================
        private async Task LoadProductsAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                Products = new();
                FollowingStoreIds = new();
                return;
            }

            FollowingStoreIds = await _context.StoreFollows
                .Where(f => f.CustomerID == customerId.Value)
                .Select(f => f.StoreID)
                .ToListAsync();

            Products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Store)
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
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