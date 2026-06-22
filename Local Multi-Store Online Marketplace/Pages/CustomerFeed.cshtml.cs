using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerFeedModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;
        private readonly CustomerManager _customerManager;
        private readonly MessagingManager _messagingManager;
        private readonly WishlistManager _wishlistManager;
        private readonly ApplicationDbContext _context;
        
        public CustomerFeedModel(
    StoreManager storeManager,
    UserManager<User> userManager,
    CustomerManager customerManager,
    MessagingManager messagingManager,
    WishlistManager wishlistManager,
    ApplicationDbContext context)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
            _messagingManager = messagingManager;
            _wishlistManager = wishlistManager;
            
            _context = context;
        }
        public List<string> NavbarCategories { get; set; } = new()
{
    "All",
    "Electronics",
    "Fashion",
    "Home",
    "Beauty",
    "Food",
    "jewelery",
    "Sports",
    "Books",
    "Pets",
    "Automotive"
};
      

        public string? SelectedCategory { get; set; }
        public List<Product> Products { get; set; } = new();

        // ? NEW: used for follow/unfollow UI
        public List<int> FollowingStoreIds { get; set; } = new();

        public async Task OnGetAsync(string? category)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return;

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
                return;

            SelectedCategory = category;

            Products = await _storeManager.GetFeedProductsAsync(customer.CustomerID);
            FollowingStoreIds = await _context.StoreFollows
    .Where(f => f.CustomerID == customer.CustomerID)
    .Select(f => f.StoreID)
    .ToListAsync();

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                Products = Products
                    .Where(p => p.Category != null &&
                                p.Category.CategoryName.Equals(
                                    category,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        // =========================
        // SHARE TO STORE CHAT (UNCHANGED)
        // =========================
        public async Task<IActionResult> OnPostShareToStoreAsync(
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
        // FOLLOW STORE (NEW)
        // =========================
        public async Task<IActionResult> OnPostFollowStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var exists = await _context.StoreFollows
                .AnyAsync(x =>
                    x.CustomerID == customer.CustomerID &&
                    x.StoreID == storeId);

            if (!exists)
            {
                _context.StoreFollows.Add(new StoreFollow
                {
                    CustomerID = customer.CustomerID,
                    StoreID = storeId,
                    FollowedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // =========================
        // UNFOLLOW STORE (NEW)
        // =========================
        public async Task<IActionResult> OnPostUnfollowStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(x =>
                    x.CustomerID == customer.CustomerID &&
                    x.StoreID == storeId);

            if (follow != null)
            {
                _context.StoreFollows.Remove(follow);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostAddReviewAsync(
    int productId,
    int rating,
    string comment)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage();

            var customer =
                await _customerManager.GetCustomerByUserIdAsync(user.Id);

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

            return RedirectToPage();
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
                .FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            var exists = await _wishlistManager.IsInWishlistAsync(
                customer.CustomerID,
                productId);

            if (exists)
            {
                TempData["Error"] = "This product is already in your wishlist.";
                return RedirectToPage();
            }

            await _wishlistManager.AddToWishlistAsync(
                customer.CustomerID,
                productId);

            TempData["Success"] = $"{product.ProductName} added to wishlist.";

            return RedirectToPage();
        }
    }
}