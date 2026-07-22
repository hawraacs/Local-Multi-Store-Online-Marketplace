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
        private readonly StoryManager _storyManager;
        private readonly BoostManager _boostManager;   // ADD THIS

        public CustomerFeedModel(
            StoreManager storeManager,
            UserManager<User> userManager,
            CustomerManager customerManager,
            MessagingManager messagingManager,
            WishlistManager wishlistManager,
            ApplicationDbContext context,
            StoryManager storyManager,
            BoostManager boostManager)                  // ADD THIS
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
            _messagingManager = messagingManager;
            _wishlistManager = wishlistManager;
            _context = context;
            _storyManager = storyManager;
            _boostManager = boostManager;                // ADD THIS
        }

        public List<string> NavbarCategories { get; set; } = new();

        public string? SelectedCategory { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<int> FollowingStoreIds { get; set; } = new();
        public List<FeedCategoryFilterViewModel> FilterCategories { get; set; } = new();
        public List<FeedStoreFilterViewModel> FilterStores { get; set; } = new();
        public List<string> FilterAreas { get; set; } = new();

        // NEW — active-boosted product IDs, used by the view to show the "Boosted" badge
        public HashSet<int> BoostedProductIds { get; set; } = new();

        // New: story circles for the top of the Feed - only from stores this customer follows
        public List<StoryGroupDTO> FollowedStoryGroups { get; set; } = new();

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

            var followedStories = await _storyManager.GetFollowedStoriesAsync(customer.CustomerID);
            var viewedStoryIds = await _storyManager.GetViewedStoryIdsAsync(customer.CustomerID);

            var likedStoryIds = new List<int>();
            foreach (var s in followedStories)
            {
                if (await _storyManager.IsLikedByCustomerAsync(s.StoryID, customer.CustomerID))
                    likedStoryIds.Add(s.StoryID);
            }

            FollowedStoryGroups = followedStories
                .GroupBy(s => s.StoreID)
                .Select(g => new StoryGroupDTO
                {
                    StoreID = g.Key,
                    StoreName = g.First().Store.StoreName,
                    StoreLogoUrl = g.First().Store.LogoURL,
                    // g is already ordered oldest -> newest by the repository - keep that order for correct playback
                    Stories = g.Select(s => new StoryDTO
                    {
                        StoryID = s.StoryID,
                        StoreID = s.StoreID,
                        MediaType = s.MediaType,
                        ImageUrl = s.ImageUrl,
                        VideoUrl = s.VideoUrl,
                        DurationSeconds = s.DurationSeconds,
                        Caption = s.Caption,
                        CreatedAt = s.CreatedAt,
                        IsViewed = viewedStoryIds.Contains(s.StoryID),
                        IsLikedByCurrentCustomer = likedStoryIds.Contains(s.StoryID)
                    })
                        .ToList()
                })
                .Select(group =>
                {
                    group.HasUnviewedStories = group.Stories.Any(s => !s.IsViewed);
                    return group;
                })
                // Which STORE's circle shows first: newest story per store, descending
                .OrderByDescending(g => g.Stories.Max(s => s.CreatedAt))
                .ToList();

            ViewMode = ShowingAllProducts ? "All" : "Following";
            SelectedCategory = category;

            NavbarCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => c.CategoryName)
                .Distinct()
                .ToListAsync();

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

            // NEW — expire any due boosts, then fetch active boosted product IDs
            await _boostManager.ExpireDueBoostsAsync();
            BoostedProductIds = await _boostManager.GetActiveBoostedProductIdsAsync();

            var unordered = await query.ToListAsync();

            // NEW — boosted products first, then newest first (same as before)
            Products = unordered
                .OrderByDescending(p => BoostedProductIds.Contains(p.ProductID))
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.ProductID)
                .ToList();
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

        // ================= ADD REVIEW =================
        public async Task<IActionResult> OnPostAddReviewAsync(int productId, int rating, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            if (rating < 1 || rating > 5 || string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "Please provide a rating between 1 and 5 and a comment.";
                return RedirectToPage();
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);
            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            _context.Reviews.Add(new Review
            {
                ProductID = productId,
                StoreID = product.StoreID,   // required FK - pulled from the product being reviewed
                CustomerID = customer.CustomerID,
                Rating = rating,
                Comment = comment.Trim(),
                Status = "Approved",          // matches your existing .Where(r => r.Status == "Approved") filter
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Thanks for your review!";
            return RedirectToPage();
        }
        // ================= STORY VIEWED (NEW) =================
        public async Task<IActionResult> OnPostMarkStoryViewedAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return new JsonResult(new { success = false });

            await _storyManager.MarkStoryViewedAsync(storyId, customer.CustomerID);
            return new JsonResult(new { success = true });
        }

        // ================= STORY LIKE / UNLIKE (NEW) =================
        public async Task<IActionResult> OnPostToggleStoryLikeAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return new JsonResult(new { success = false });

            var alreadyLiked = await _storyManager.IsLikedByCustomerAsync(storyId, customer.CustomerID);

            if (alreadyLiked)
                await _storyManager.UnlikeStoryAsync(storyId, customer.CustomerID);
            else
                await _storyManager.LikeStoryAsync(storyId, customer.CustomerID);

            var likeCount = await _storyManager.GetLikeCountAsync(storyId);

            return new JsonResult(new { success = true, liked = !alreadyLiked, likeCount });
        }

        // ================= STORY REPLY (NEW) - reuses the existing chat system =================
        public async Task<IActionResult> OnPostReplyToStoryAsync(int storyId, string replyText)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Please log in to reply." });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return new JsonResult(new { success = false, error = "Customer account required." });

            if (string.IsNullOrWhiteSpace(replyText))
                return new JsonResult(new { success = false, error = "Reply cannot be empty." });

            var story = await _storyManager.GetByIdWithStoreAsync(storyId);
            if (story == null)
                return new JsonResult(new { success = false, error = "Story not found." });

            // A customer replying to their own store's story would be a Store Owner
            // replying to themselves - not a valid customer action.
            if (story.Store.OwnerUserID == user.Id)
                return new JsonResult(new { success = false, error = "You cannot reply to your own story." });

            await _messagingManager.SendStoryReplyAsync(user.Id, story.Store.OwnerUserID, storyId, replyText);

            return new JsonResult(new { success = true });
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