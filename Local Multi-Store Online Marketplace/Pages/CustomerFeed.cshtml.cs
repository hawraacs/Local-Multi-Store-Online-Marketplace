using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
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
        private readonly BoostManager _boostManager;
        // NEW — used to pull the customer's real promotions for the right
        // sidebar banner (see FeaturedPromotion below), same source the
        // CustomerPromotions page uses.
        private readonly IPromotionManager _promotionManager;

        public CustomerFeedModel(
            StoreManager storeManager,
            UserManager<User> userManager,
            CustomerManager customerManager,
            MessagingManager messagingManager,
            WishlistManager wishlistManager,
            ApplicationDbContext context,
            StoryManager storyManager,
            BoostManager boostManager,
            IPromotionManager promotionManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
            _messagingManager = messagingManager;
            _wishlistManager = wishlistManager;
            _context = context;
            _storyManager = storyManager;
            _boostManager = boostManager;
            _promotionManager = promotionManager;
        }

        public List<string> NavbarCategories { get; set; } = new();

        public string? SelectedCategory { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<int> FollowingStoreIds { get; set; } = new();
        public List<FeedCategoryFilterViewModel> FilterCategories { get; set; } = new();
        public List<FeedStoreFilterViewModel> FilterStores { get; set; } = new();
        public List<string> FilterAreas { get; set; } = new();

        public HashSet<int> BoostedProductIds { get; set; } = new();

        public int CurrentCustomerId { get; set; }

        public List<StoryGroupDTO> FollowedStoryGroups { get; set; } = new();

        // NEW — whether the current customer already owns a store, so the
        // sidebar "Become a Seller" button can route to the seller
        // dashboard instead of the signup/request page (mirrors Customer1).
        public bool CustomerHasStore { get; set; }

        // NEW — a real promotion to show in the right-sidebar banner
        // instead of the old hardcoded "Up to 60% Off" placeholder.
        // Picks the customer's most relevant unread promotion (falling
        // back to the most recent one if everything is already read).
        public PromotionRecipient? FeaturedPromotion { get; set; }

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

            CurrentCustomerId = customer.CustomerID;

            // NEW — used by the sidebar "Become a Seller" button.
            CustomerHasStore = await _context.Stores
                .AnyAsync(s => s.OwnerUserID == user.Id);

            // NEW — real promotion for the right-sidebar banner.
            var customerPromotions = await _promotionManager.GetCustomerPromotionsAsync(user.Id);
            FeaturedPromotion = customerPromotions
                .OrderBy(p => p.IsRead)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefault();

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

            await _boostManager.ExpireDueBoostsAsync();
            BoostedProductIds = await _boostManager.GetActiveBoostedProductIdsAsync();

            var unordered = await query.ToListAsync();

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

        private static string GetDisplayName(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.FullName)) return user.FullName;
            if (!string.IsNullOrWhiteSpace(user.UserName)) return user.UserName;
            return "A customer";
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

                var ownerUserId = await _context.Stores
                    .Where(s => s.StoreID == storeId)
                    .Select(s => (int?)s.OwnerUserID)
                    .FirstOrDefaultAsync();

                if (ownerUserId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserID = ownerUserId.Value,
                        Title = "New follower",
                        Message = $"{GetDisplayName(user)} started following your store.",
                        Type = "Follow",
                        ReferenceID = storeId,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        SentVia = "System"
                    });
                }

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

        // ================= BLOCK (auto-unfollows) =================
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

                // Block always implies unfollow, and it stays unfollowed
                // until the customer explicitly follows again.
                var follow = await _context.StoreFollows.FirstOrDefaultAsync(f =>
                    f.CustomerID == customer.CustomerID && f.StoreID == storeId);

                if (follow != null)
                    _context.StoreFollows.Remove(follow);

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

        // ================= ADD TO CART (redirects to the cart page) =================
        // NEW — previously this button posted to Customer1's AddToCart
        // handler, which redirected back to Customer1. It now lives on
        // this page and always lands the customer on /CustomerCart, since
        // "Shop Now" should take them straight to checkout.
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

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
                .FirstOrDefaultAsync(item =>
                    item.CartID == cart.CartID &&
                    item.ProductID == productId);

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

                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Whether the item was newly added or was already in the cart,
            // "Shop Now" means "take me to checkout" — always land on the
            // cart page rather than bouncing back to the feed.
            return RedirectToPage("/CustomerCart");
        }

        // ================= NOT INTERESTED (AJAX) =================
        public async Task<IActionResult> OnPostNotInterestedAsync(int productId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return new JsonResult(new { success = false, message = "Please login as a customer first." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
            {
                return new JsonResult(new { success = false, message = "Customer account required." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

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

            return new JsonResult(new { success = true, message = "We'll show fewer items like this." });
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

            var review = new Review
            {
                ProductID = productId,
                StoreID = product.StoreID,
                CustomerID = customer.CustomerID,
                Rating = rating,
                Comment = comment.Trim(),
                Status = "Approved",
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            var ownerUserId = await _context.Stores
                .Where(s => s.StoreID == product.StoreID)
                .Select(s => s.OwnerUserID)
                .FirstOrDefaultAsync();

            _context.Notifications.Add(new Notification
            {
                UserID = ownerUserId,
                Title = "New product review",
                Message = $"{GetDisplayName(user)} left a {rating}-star review on {product.ProductName}: \"{review.Comment}\"",
                Type = "ProductReview",
                ReferenceID = review.ReviewID,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = "System"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Thanks for your review!";
            return RedirectToPage();
        }

        // ================= DELETE REVIEW =================
        public async Task<IActionResult> OnPostDeleteReviewAsync(int reviewId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage();

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage();

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewID == reviewId);
            if (review == null)
            {
                TempData["Error"] = "That review could not be found - it may have already been removed.";
                return RedirectToPage();
            }

            if (review.CustomerID != customer.CustomerID)
            {
                TempData["Error"] = "You can only delete your own review.";
                return RedirectToPage();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your review has been removed.";
            return RedirectToPage();
        }

        // ================= STORY VIEWED =================
        public async Task<IActionResult> OnPostMarkStoryViewedAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return new JsonResult(new { success = false });

            await _storyManager.MarkStoryViewedAsync(storyId, customer.CustomerID);
            return new JsonResult(new { success = true });
        }

        // ================= STORY LIKE / UNLIKE =================
        public async Task<IActionResult> OnPostToggleStoryLikeAsync(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return new JsonResult(new { success = false });

            var alreadyLiked = await _storyManager.IsLikedByCustomerAsync(storyId, customer.CustomerID);

            var story = await _storyManager.GetByIdWithStoreAsync(storyId);

            if (alreadyLiked)
            {
                await _storyManager.UnlikeStoryAsync(storyId, customer.CustomerID);
            }
            else
            {
                await _storyManager.LikeStoryAsync(storyId, customer.CustomerID);

                if (story != null && story.Store != null && story.Store.OwnerUserID != user.Id)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserID = story.Store.OwnerUserID,
                        Title = "New story like",
                        Message = $"{GetDisplayName(user)} liked your story.",
                        Type = "StoryLike",
                        ReferenceID = storyId,
                        IsRead = false,
                        SentAt = DateTime.UtcNow,
                        SentVia = "System"
                    });

                    await _context.SaveChangesAsync();
                }
            }

            var likeCount = await _storyManager.GetLikeCountAsync(storyId);

            return new JsonResult(new { success = true, liked = !alreadyLiked, likeCount });
        }

        // ================= STORY REPLY =================
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

            if (story.Store.OwnerUserID == user.Id)
                return new JsonResult(new { success = false, error = "You cannot reply to your own story." });

            await _messagingManager.SendStoryReplyAsync(user.Id, story.Store.OwnerUserID, storyId, replyText);

            _context.Notifications.Add(new Notification
            {
                UserID = story.Store.OwnerUserID,
                Title = "New story reply",
                Message = $"{GetDisplayName(user)} replied to your story: \"{replyText.Trim()}\"",
                Type = "StoryReply",
                ReferenceID = storyId,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = "System"
            });

            await _context.SaveChangesAsync();

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