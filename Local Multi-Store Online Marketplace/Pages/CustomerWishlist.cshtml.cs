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

        // ---------------------------------------------------------------
        // "You might also like" — a small set of real, active products
        // pulled directly from the same Products table Explore/Feed use,
        // excluding anything already in the wishlist. No new recommendation
        // engine/table — this is the smallest query that gets real data
        // onto the page (see chat notes for details).
        // ---------------------------------------------------------------
        public List<WishlistRecommendationViewModel> Recommendations { get; set; } = new();

        // ---------------------------------------------------------------
        // Derived, real-data summary values for the "Wishlist Summary"
        // card. Nothing here is stored — it's computed from WishlistItems
        // on every page load, so it can never drift from the real data.
        // ---------------------------------------------------------------
        public int ItemsSavedCount => WishlistItems.Count;

        public decimal PriceEstimate => WishlistItems.Sum(i => i.Price);

        public int EligibleForCartCount => WishlistItems.Count(i => !i.IsOutOfStock);

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

            await LoadRecommendationsAsync();

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

            var result = await MoveSingleItemToCartAsync(customerId.Value, productId, quantity);

            TempData[result.Success ? "Success" : "Error"] = result.Message;

            return RedirectToPage();
        }

        // ---------------------------------------------------------------
        // MOVE ALL TO CART — used by BOTH "Move All to Cart" buttons
        // (Wishlist Summary card + "Don't miss out" banner). Both post
        // to this single handler, so there is exactly one implementation
        // of the bulk action, reusing the same per-item logic as the
        // single-product "Add to Cart" button above.
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostMoveAllToCartAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var wishlistItems = await _wishlistManager
                .GetCustomerWishlistAsync(customerId.Value);

            var eligibleItems = wishlistItems
                .Where(i => !i.IsOutOfStock)
                .ToList();

            if (!eligibleItems.Any())
            {
                TempData["Error"] = "None of your wishlist items are currently in stock to move to your cart.";
                return RedirectToPage();
            }

            var movedCount = 0;
            var skippedCount = 0;

            foreach (var item in eligibleItems)
            {
                var result = await MoveSingleItemToCartAsync(customerId.Value, item.ProductID, 1);

                if (result.Success)
                {
                    movedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (movedCount > 0 && skippedCount == 0)
            {
                TempData["Success"] = movedCount == 1
                    ? "1 item moved to your cart."
                    : $"{movedCount} items moved to your cart.";
            }
            else if (movedCount > 0 && skippedCount > 0)
            {
                TempData["Success"] = $"{movedCount} item(s) moved to your cart. {skippedCount} item(s) could not be moved.";
            }
            else
            {
                TempData["Error"] = "We couldn't move any items to your cart right now.";
            }

            return RedirectToPage();
        }

        // ---------------------------------------------------------------
        // ADD TO CART for a "You might also like" recommendation.
        // Unlike the wishlist row's "Add to Cart" (which moves the item
        // OUT of the wishlist), a recommendation isn't in the wishlist,
        // so this only adds to the real Cart and leaves the wishlist
        // untouched. Mirrors the existing add-to-cart logic used
        // elsewhere in the app (see Customer1's AddProductToCartInternalAsync).
        // ---------------------------------------------------------------
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
                    p.IsActive &&
                    p.Quantity > 0);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
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
                existingItem.Quantity = Math.Min(existingItem.Quantity + 1, product.Quantity);
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    CartID = cart.CartID,
                    ProductID = productId,
                    Quantity = 1,
                    PriceAtAddTime = product.Price,
                    AddedAt = DateTime.UtcNow
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{product.ProductName} added to your cart.";

            return RedirectToPage();
        }

        // ---------------------------------------------------------------
        // WISHLIST (heart) action for a "You might also like" recommendation.
        // Reuses the same WishlistManager as every other page in the app.
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            if (await _wishlistManager.IsInWishlistAsync(customerId.Value, productId))
            {
                TempData["Error"] = "This product is already in your wishlist.";
                return RedirectToPage();
            }

            await _wishlistManager.AddToWishlistAsync(customerId.Value, productId);

            TempData["Success"] = $"{product.ProductName} added to your wishlist.";

            return RedirectToPage();
        }

        // ---------------------------------------------------------------
        // Shared single-item "move to cart" logic used by both the
        // per-row "Add to Cart" button and the bulk "Move All to Cart"
        // handler above, so there is exactly one implementation of the
        // real move-to-cart behavior.
        // ---------------------------------------------------------------
        private async Task<(bool Success, string Message)> MoveSingleItemToCartAsync(
            int customerId,
            int productId,
            int quantity)
        {
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
                return (false, "Product is not available.");
            }

            if (quantity > product.Quantity)
            {
                quantity = product.Quantity;
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerID = customerId,
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

                await _wishlistManager.RemoveFromWishlistAsync(customerId, productId);

                return (true, $"{product.ProductName} quantity updated in your cart.");
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

            await _wishlistManager.RemoveFromWishlistAsync(customerId, productId);

            return (true, $"{product.ProductName} moved to your cart.");
        }

        // ---------------------------------------------------------------
        // Loads a small set of real, active products for "You might also
        // like", using the exact same Product/Images query shape already
        // used on Explore/Feed (see Customer1_cshtml.cs), so the image
        // picked here is the same "primary image" every other page shows.
        // ---------------------------------------------------------------
        private async Task LoadRecommendationsAsync()
        {
            var wishlistProductIds = WishlistItems
                .Select(i => i.ProductID)
                .ToList();

            Recommendations = await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Store)
                .Where(p =>
                    p.IsActive &&
                    p.Quantity > 0 &&
                    p.Store != null &&
                    p.Store.Status == "Approved" &&
                    !wishlistProductIds.Contains(p.ProductID))
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .Select(p => new WishlistRecommendationViewModel
                {
                    ProductID = p.ProductID,
                    StoreID = p.StoreID,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    ImageUrl = p.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/no-image.png"
                })
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

    // ---------------------------------------------------------------
    // Small, page-local view model for "You might also like". Only
    // carries fields that are real (ProductID/StoreID/ProductName/
    // Price/ImageUrl) — nothing here is invented.
    // ---------------------------------------------------------------
    public class WishlistRecommendationViewModel
    {
        public int ProductID { get; set; }
        public int StoreID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
