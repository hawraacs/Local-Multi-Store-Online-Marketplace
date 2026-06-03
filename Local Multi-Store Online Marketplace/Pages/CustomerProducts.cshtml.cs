using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerProductsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly WishlistManager _wishlistManager;
        private readonly UserManager<User> _userManager;

        public CustomerProductsModel(
            ApplicationDbContext context,
            WishlistManager wishlistManager,
            UserManager<User> userManager)
        {
            _context = context;
            _wishlistManager = wishlistManager;
            _userManager = userManager;
        }

        public List<ProductDisplayViewModel> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            Products = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Store)
                .Where(p => p.IsActive && p.Quantity > 0)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProductDisplayViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    Description = p.Description,
                    PrimaryImageUrl = p.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/no-image.png",
                    StoreName = p.Store != null ? p.Store.StoreName : "Unknown Store",
                    CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized"
                })
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            try
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

                await _wishlistManager.AddToWishlistAsync(
                    customerId.Value,
                    productId);

                TempData["Success"] = $"{product.ProductName} added to your wishlist!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddCartAsync(int productId)
        {
            try
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

                var cart = await _context.Set<Cart>()
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

                    _context.Set<Cart>().Add(cart);
                    await _context.SaveChangesAsync();
                }

                var existingItem = await _context.Set<CartItem>()
                    .FirstOrDefaultAsync(ci =>
                        ci.CartID == cart.CartID &&
                        ci.ProductID == productId);

                if (existingItem != null)
                {
                    if (existingItem.Quantity + 1 > product.Quantity)
                    {
                        TempData["Error"] = "Not enough stock available.";
                        return RedirectToPage();
                    }

                    existingItem.Quantity += 1;
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        CartID = cart.CartID,
                        ProductID = productId,
                        Quantity = 1,
                        PriceAtAddTime = product.Price,
                        AddedAt = DateTime.UtcNow
                    };

                    _context.Set<CartItem>().Add(cartItem);
                }

                cart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"{product.ProductName} added to your cart!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

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

    public class ProductDisplayViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;

        public string PrimaryImageUrl { get; set; } = "/images/no-image.png";

        public string StoreName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
    }
}