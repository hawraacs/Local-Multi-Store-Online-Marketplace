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

        public List<CategoryFilterViewModel> Categories { get; set; } = new();

        public List<StoreFilterViewModel> Stores { get; set; } = new();

        public List<string> Areas { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CategoryId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StoreId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Area { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MinPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal? MaxPrice { get; set; }

        public async Task OnGetAsync()
        {
            await LoadFiltersAsync();

            var query = _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Store)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.Trim();

                query = query.Where(p =>
                    p.ProductName.Contains(search) ||
                    p.Description.Contains(search) ||
                    (p.Store != null && p.Store.StoreName.Contains(search)) ||
                    (p.Category != null && p.Category.CategoryName.Contains(search)));
            }

            if (CategoryId.HasValue && CategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryID == CategoryId.Value);
            }

            if (StoreId.HasValue && StoreId.Value > 0)
            {
                query = query.Where(p => p.StoreID == StoreId.Value);
            }

            if (!string.IsNullOrWhiteSpace(Area))
            {
                var selectedArea = Area.Trim();

                query = query.Where(p =>
                    p.Store != null &&
                    p.Store.Area == selectedArea);
            }

            if (MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= MinPrice.Value);
            }

            if (MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= MaxPrice.Value);
            }

            Products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProductDisplayViewModel
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    Quantity = p.Quantity,
                    Description = p.Description,
                    PrimaryImageUrl = p.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/no-image.png",
                    StoreName = p.Store != null
                        ? p.Store.StoreName
                        : "Unknown Store",
                    CategoryName = p.Category != null
                        ? p.Category.CategoryName
                        : "Uncategorized"
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
                        p.IsActive);

                if (product == null)
                {
                    TempData["Error"] = "Product is not available.";
                    return RedirectToPage(GetCurrentFiltersRouteValues());
                }

                var alreadyInWishlist = await _wishlistManager.IsInWishlistAsync(
                    customerId.Value,
                    productId);

                if (alreadyInWishlist)
                {
                    TempData["Error"] = "This product is already in your wishlist.";
                    return RedirectToPage(GetCurrentFiltersRouteValues());
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

            return RedirectToPage(GetCurrentFiltersRouteValues());
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
                        p.IsActive);

                if (product == null)
                {
                    TempData["Error"] = "Product is not available.";
                    return RedirectToPage(GetCurrentFiltersRouteValues());
                }

                if (product.Quantity <= 0)
                {
                    TempData["Error"] = "This product is out of stock.";
                    return RedirectToPage(GetCurrentFiltersRouteValues());
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
                    return RedirectToPage(GetCurrentFiltersRouteValues());
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
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage(GetCurrentFiltersRouteValues());
        }

        private async Task LoadFiltersAsync()
        {
            Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryFilterViewModel
                {
                    CategoryID = c.CategoryID,
                    CategoryName = c.CategoryName
                })
                .ToListAsync();

            Stores = await _context.Stores
                .OrderBy(s => s.StoreName)
                .Select(s => new StoreFilterViewModel
                {
                    StoreID = s.StoreID,
                    StoreName = s.StoreName
                })
                .ToListAsync();

            Areas = await _context.Stores
                .Where(s => s.Area != null && s.Area != "")
                .Select(s => s.Area!)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        private object GetCurrentFiltersRouteValues()
        {
            return new
            {
                searchTerm = SearchTerm,
                categoryId = CategoryId,
                storeId = StoreId,
                area = Area,
                minPrice = MinPrice,
                maxPrice = MaxPrice
            };
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

        public int Quantity { get; set; }

        public string Description { get; set; } = string.Empty;

        public string PrimaryImageUrl { get; set; } = "/images/no-image.png";

        public string StoreName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public bool IsOutOfStock => Quantity <= 0;
    }

    public class CategoryFilterViewModel
    {
        public int CategoryID { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }

    public class StoreFilterViewModel
    {
        public int StoreID { get; set; }

        public string StoreName { get; set; } = string.Empty;
    }
}