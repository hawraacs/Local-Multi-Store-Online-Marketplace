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
    public class CustomerRecentlyViewedModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly WishlistManager _wishlistManager;

        public CustomerRecentlyViewedModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            WishlistManager wishlistManager)
        {
            _context = context;
            _userManager = userManager;
            _wishlistManager = wishlistManager;
        }

        public List<CustomerRecentlyViewedViewModel> Products { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Products = await _context.RecentlyViewedProducts
                .Where(rv => rv.CustomerID == customerId.Value)
                .Join(
                    _context.Products
                        .Include(p => p.Images)
                        .Include(p => p.Store)
                        .Include(p => p.Category)
                        .Where(p => p.IsActive),
                    rv => rv.ProductID,
                    p => p.ProductID,
                    (rv, p) => new CustomerRecentlyViewedViewModel
                    {
                        ProductID = p.ProductID,
                        ProductName = p.ProductName,
                        Price = p.Price,
                        Quantity = p.Quantity,
                        StoreName = p.Store != null
                            ? p.Store.StoreName
                            : "Unknown Store",
                        CategoryName = p.Category != null
                            ? p.Category.CategoryName
                            : "Uncategorized",
                        ViewedAt = rv.ViewedAt,
                        ImageUrl = p.Images
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.DisplayOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault() ?? "/images/no-image.png"
                    })
                .OrderByDescending(x => x.ViewedAt)
                .Take(10)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAddToWishlistAsync(int productId)
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

            TempData["Success"] = $"{product.ProductName} added to your wishlist.";

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

    public class CustomerRecentlyViewedViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = "/images/no-image.png";

        public DateTime ViewedAt { get; set; }

        public bool IsOutOfStock => Quantity <= 0;
    }
}