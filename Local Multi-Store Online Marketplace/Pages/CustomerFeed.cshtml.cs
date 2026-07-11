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
            "All","Electronics","Fashion","Home","Beauty","Food",
            "jewelery","Sports","Books","Pets","Automotive"
        };

        public string? SelectedCategory { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<int> FollowingStoreIds { get; set; } = new();
        public List<FeedCategoryFilterViewModel> FilterCategories { get; set; } = new();
        public List<FeedStoreFilterViewModel> FilterStores { get; set; } = new();
        public List<string> FilterAreas { get; set; } = new();

        [BindProperty(SupportsGet = true)] public string ViewMode { get; set; } = "Following";
        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
        [BindProperty(SupportsGet = true)] public int? StoreId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Area { get; set; }
        [BindProperty(SupportsGet = true)] public decimal? MinPrice { get; set; }
        [BindProperty(SupportsGet = true)] public decimal? MaxPrice { get; set; }

        public bool ShowingAllProducts => string.Equals(ViewMode, "All", StringComparison.OrdinalIgnoreCase);

        public async Task OnGetAsync(string? category)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return;

            ViewMode = ShowingAllProducts ? "All" : "Following";
            SelectedCategory = category;

            await LoadFilterOptionsAsync();

            FollowingStoreIds = await _context.StoreFollows
                .Where(f => f.CustomerID == customer.CustomerID)
                .Select(f => f.StoreID)
                .ToListAsync();

            var blockedUserIds = await _context.BlockRelations
                .Where(b => b.BlockerUserId == customer.UserID)
                .Select(b => b.BlockedUserId)
                .ToListAsync();

            var hiddenProductIds = await _context.ProductHides
                .Where(x => x.CustomerId == customer.CustomerID)
                .Select(x => x.ProductId)
                .ToListAsync();

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Store)
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.Customer)
                        .ThenInclude(c => c.User)
                .Where(p =>
                    p.IsActive &&
                    p.Store != null &&
                    p.Store.Status == "Approved" &&
                    !hiddenProductIds.Contains(p.ProductID) &&
                    !blockedUserIds.Contains(p.Store.OwnerUserID));

            if (!ShowingAllProducts)
            {
                query = query.Where(p => FollowingStoreIds.Contains(p.StoreID));
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(p =>
                    p.Category != null &&
                    p.Category.CategoryName == category);
            }

            var search = SearchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.ProductName.Contains(search) ||
                    p.Description.Contains(search) ||
                    p.Store.StoreName.Contains(search) ||
                    (p.Category != null && p.Category.CategoryName.Contains(search)));
            }

            if (CategoryId.HasValue && CategoryId.Value > 0)
                query = query.Where(p => p.CategoryID == CategoryId.Value);

            if (StoreId.HasValue && StoreId.Value > 0)
                query = query.Where(p => p.StoreID == StoreId.Value);

            if (!string.IsNullOrWhiteSpace(Area))
            {
                var selectedArea = Area.Trim();
                query = query.Where(p => p.Store.Area == selectedArea);
            }

            if (MinPrice.HasValue)
                query = query.Where(p => p.Price >= MinPrice.Value);

            if (MaxPrice.HasValue)
                query = query.Where(p => p.Price <= MaxPrice.Value);

            Products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.ProductID)
                .ToListAsync();
        }

        private async Task LoadFilterOptionsAsync()
        {
            FilterCategories = await _context.Categories.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => new FeedCategoryFilterViewModel
                {
                    CategoryID = c.CategoryID,
                    CategoryName = c.CategoryName
                })
                .ToListAsync();

            FilterStores = await _context.Stores.AsNoTracking()
                .Where(s => s.Status == "Approved")
                .OrderBy(s => s.StoreName)
                .Select(s => new FeedStoreFilterViewModel
                {
                    StoreID = s.StoreID,
                    StoreName = s.StoreName
                })
                .ToListAsync();

            FilterAreas = await _context.Stores.AsNoTracking()
                .Where(s => s.Status == "Approved" && s.Area != null && s.Area != "")
                .Select(s => s.Area!)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        // ================= FOLLOW =================
        public async Task<IActionResult> OnPostFollowStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var exists = await _context.StoreFollows
                .AnyAsync(x => x.CustomerID == customer.CustomerID && x.StoreID == storeId);

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

        // ================= UNFOLLOW =================
        public async Task<IActionResult> OnPostUnfollowStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(x => x.CustomerID == customer.CustomerID && x.StoreID == storeId);

            if (follow != null)
            {
                _context.StoreFollows.Remove(follow);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // ================= BLOCK (FIXED) =================
        public async Task<IActionResult> OnPostBlockPostAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.StoreID == storeId);
            if (store == null) return RedirectToPage();

            var exists = await _context.BlockRelations.AnyAsync(x =>
                x.BlockerUserId == customer.UserID &&
                x.BlockedUserId == store.OwnerUserID);

            if (!exists)
            {
                _context.BlockRelations.Add(new BlockRelation
                {
                    BlockerUserId = customer.UserID,
                    BlockedUserId = store.OwnerUserID,
                    BlockerRole = "Customer",
                    BlockedRole = "Store"
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // ================= OTHER FEATURES (UNCHANGED) =================
        public async Task<IActionResult> OnPostShareToStoreAsync(int productId, int storeOwnerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            await _messagingManager.SendProductAsync(user.Id, storeOwnerId, productId);

            TempData["Success"] = "Product shared successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);
            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            await _wishlistManager.AddToWishlistAsync(customer.CustomerID, productId);

            TempData["Success"] = $"{product.ProductName} added to wishlist.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostNotInterestedAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var exists = await _context.ProductHides.AnyAsync(x =>
                x.CustomerId == customer.CustomerID &&
                x.ProductId == productId);

            if (!exists)
            {
                _context.ProductHides.Add(new ProductHide
                {
                    CustomerId = customer.CustomerID,
                    ProductId = productId,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "We will show fewer similar items.";
            return RedirectToPage();
        }

        public IActionResult OnPostReportPost(int productId)
        {
            TempData["Success"] = "Report submitted. Our team will review it.";
            return RedirectToPage();
        }
    }

    public class FeedCategoryFilterViewModel
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class FeedStoreFilterViewModel
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
    }

}