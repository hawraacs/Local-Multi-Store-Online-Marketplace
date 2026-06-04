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
    public class CustomerProductDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly WishlistManager _wishlistManager;

        public CustomerProductDetailsModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            WishlistManager wishlistManager)
        {
            _context = context;
            _userManager = userManager;
            _wishlistManager = wishlistManager;
        }

        public CustomerProductDetailsViewModel Product { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p =>
                    p.ProductID == id &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product was not found.";
                return RedirectToPage("/CustomerProducts");
            }

            await SaveRecentlyViewedAsync(customerId.Value, product.ProductID);

            Product = new CustomerProductDetailsViewModel
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                Quantity = product.Quantity,
                StoreName = product.Store != null ? product.Store.StoreName : "Unknown Store",
                CategoryName = product.Category != null ? product.Category.CategoryName : "Uncategorized",
                ImageUrl = product.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault() ?? "/images/no-image.png"
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAddCartAsync(int productId)
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
                return RedirectToPage("/CustomerProducts");
            }

            if (product.Quantity <= 0)
            {
                TempData["Error"] = "This product is out of stock.";
                return RedirectToPage(new { id = productId });
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
                return RedirectToPage(new { id = productId });
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

            TempData["Success"] = $"{product.ProductName} added to your cart.";

            return RedirectToPage(new { id = productId });
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
                return RedirectToPage("/CustomerProducts");
            }

            var alreadyInWishlist = await _wishlistManager.IsInWishlistAsync(
                customerId.Value,
                productId);

            if (alreadyInWishlist)
            {
                TempData["Error"] = "This product is already in your wishlist.";
                return RedirectToPage(new { id = productId });
            }

            await _wishlistManager.AddToWishlistAsync(
                customerId.Value,
                productId);

            TempData["Success"] = $"{product.ProductName} added to your wishlist.";

            return RedirectToPage(new { id = productId });
        }

        private async Task SaveRecentlyViewedAsync(int customerId, int productId)
        {
            var existing = await _context.RecentlyViewedProducts
                .FirstOrDefaultAsync(x =>
                    x.CustomerID == customerId &&
                    x.ProductID == productId);

            if (existing != null)
            {
                existing.ViewedAt = DateTime.UtcNow;
            }
            else
            {
                var recent = new RecentlyViewedProduct
                {
                    CustomerID = customerId,
                    ProductID = productId,
                    ViewedAt = DateTime.UtcNow
                };

                _context.RecentlyViewedProducts.Add(recent);
            }

            await _context.SaveChangesAsync();
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

    public class CustomerProductDetailsViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public string ImageUrl { get; set; } = "/images/no-image.png";

        public string StoreName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public bool IsOutOfStock => Quantity <= 0;
    }
}