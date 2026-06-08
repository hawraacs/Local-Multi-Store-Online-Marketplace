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

        public Customer1Model(
            ApplicationDbContext context,
            UserManager<User> userManager,
            WishlistManager wishlistManager)
        {
            _context = context;
            _userManager = userManager;
            _wishlistManager = wishlistManager;
        }


        
        public List<int> FollowingStoreIds { get; set; } = new();

        public List<Product> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadProductsAsync();
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            if (product.Quantity <= 0)
            {
                TempData["Error"] = "This product is out of stock.";
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

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(ci =>
                    ci.CartID == cart.CartID &&
                    ci.ProductID == productId);

            if (existingItem != null)
            {
                TempData["Error"] = "This product is already in your cart. You can update the quantity from the cart page.";
                return RedirectToPage();
            }

            var cartItem = new CartItem
            {
                CartID = cart.CartID,
                ProductID = productId,
                Quantity = 1,
                PriceAtAddTime = product.Price,
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{product.ProductName} added to your cart!";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            var alreadyInWishlist = await _wishlistManager.IsInWishlistAsync(
                customerId.Value,
                productId);

            if (alreadyInWishlist)
            {
                TempData["Error"] = "This product is already in your wishlist.";
                return RedirectToPage();
            }

            await _wishlistManager.AddToWishlistAsync(
                customerId.Value,
                productId);

            TempData["Success"] = $"{product.ProductName} added to your wishlist!";

            return RedirectToPage();
        }

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

        public async Task<IActionResult> OnPostFollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
                return RedirectToPage();

            bool exists = await _context.StoreFollows
                .AnyAsync(x =>
                    x.CustomerID == customerId.Value &&
                    x.StoreID == storeId);

            if (!exists)
            {
                _context.StoreFollows.Add(new StoreFollow
                {
                    CustomerID = customerId.Value,
                    StoreID = storeId,
                    FollowedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                TempData["Success"] = "Store followed successfully.";
            }

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostUnfollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
                return RedirectToPage();

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == storeId);

            if (follow != null)
            {
                _context.StoreFollows.Remove(follow);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Unfollowed store";
            }

            return RedirectToPage();
        }



    }
}