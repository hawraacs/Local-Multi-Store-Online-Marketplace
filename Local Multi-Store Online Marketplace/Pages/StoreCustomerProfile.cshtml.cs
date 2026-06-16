using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;
using Multi_Store.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreCustomerProfileModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;
        private readonly CustomerManager _customerManager;
        private readonly MessagingManager _messagingManager;
        private readonly WishlistManager _wishlistManager;
        private readonly ApplicationDbContext _context;
        private readonly CartManager _cartManager;

        public StoreCustomerProfileModel(
    StoreManager storeManager,
    UserManager<User> userManager,
    CustomerManager customerManager,
    MessagingManager messagingManager,
    WishlistManager wishlistManager,
    CartManager cartManager,
    ApplicationDbContext context)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
            _messagingManager = messagingManager;
            _wishlistManager = wishlistManager;
            _cartManager = cartManager;
            _context = context;
        }

        public Store Store { get; set; }
        public List<Product> Products { get; set; } = new();

        public int FollowersCount { get; set; }
        public bool IsFollowing { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var store = await _storeManager.GetStoreByIdAsync(id);

            if (store == null)
                return NotFound();

            Store = store;
            Products = await _context.Products
     .Include(p => p.Images)
     .Include(p => p.Reviews)
         .ThenInclude(r => r.Customer)
             .ThenInclude(c => c.User)
     .Where(p => p.StoreID == id)
     .ToListAsync();
            FollowersCount = await _storeManager.GetFollowersCountAsync(id);

            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

                if (customer != null)
                {
                    IsFollowing =
                        await _storeManager.IsFollowingAsync(customer.CustomerID, id);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostFollowAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage(new { id = storeId });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage(new { id = storeId });

            await _storeManager.FollowStoreAsync(customer.CustomerID, storeId);

            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostUnfollowAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage(new { id = storeId });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage(new { id = storeId });

            await _storeManager.UnfollowStoreAsync(customer.CustomerID, storeId);

            return RedirectToPage(new { id = storeId });
        }


        public async Task<IActionResult> OnPostAddReviewAsync(
    int productId,
    int rating,
    string comment)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
                return RedirectToPage();

            var product = await _context.Products
                .FirstOrDefaultAsync(x => x.ProductID == productId);

            if (product == null)
                return RedirectToPage();

            _context.Reviews.Add(new Review
            {
                CustomerID = customer.CustomerID,
                ProductID = productId,
                StoreID = product.StoreID,
                Rating = rating,
                Comment = comment,
                Status = "Approved",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToPage(new
            {
                id = product.StoreID
            });
        }
        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
                return RedirectToPage();

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            if (product == null)
                return RedirectToPage();

            var exists = await _wishlistManager.IsInWishlistAsync(
                customer.CustomerID,
                productId);

            if (!exists)
            {
                await _wishlistManager.AddToWishlistAsync(
                    customer.CustomerID,
                    productId);
            }

            return RedirectToPage(new
            {
                id = product.StoreID
            });
        }
        public async Task<IActionResult> OnPostShareToStoreAsync(
    int productId,
    int storeOwnerId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            await _messagingManager.SendProductAsync(
                user.Id,
                storeOwnerId,
                productId);

            var product = await _context.Products
                .FirstOrDefaultAsync(x => x.ProductID == productId);

            return RedirectToPage(new
            {
                id = product?.StoreID
            });
        }
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
                return RedirectToPage();

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToPage(new { id = product?.StoreID });
            }

            if (product.Quantity <= 0)
            {
                TempData["Error"] = "Out of stock.";
                return RedirectToPage(new { id = product.StoreID });
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerID == customer.CustomerID);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerID = customer.CustomerID,
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

            if (existingItem == null)
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
            }

            TempData["Success"] = "Added to cart.";

            return RedirectToPage(new { id = product.StoreID });
        }
    }
}