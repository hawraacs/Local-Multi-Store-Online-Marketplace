using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using Multi_Store.Services.Dtos;

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
        private readonly StoryManager _storyManager;

        public StoreCustomerProfileModel(
            StoreManager storeManager,
            UserManager<User> userManager,
            CustomerManager customerManager,
            MessagingManager messagingManager,
            WishlistManager wishlistManager,
            CartManager cartManager,
            ApplicationDbContext context,
            StoryManager storyManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
            _messagingManager = messagingManager;
            _wishlistManager = wishlistManager;
            _context = context;
            _storyManager = storyManager;
        }

        public Store Store { get; set; } = null!;
        public List<Product> Products { get; set; } = new();

        // New: this store's own active stories, for the ring around its avatar
        public List<StoryDTO> StoreStories { get; set; } = new();

        public int FollowersCount { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsBlocked { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? ProductId { get; set; }
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var store = await _storeManager.GetStoreByIdAsync(id);
            if (store == null || !string.Equals(store.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            Store = store;
            Products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.Customer)
                        .ThenInclude(c => c.User)
                .Where(p => p.StoreID == id && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            FollowersCount = await _storeManager.GetFollowersCountAsync(id);

            var storeStories = await _storyManager.GetStoreStoriesAsync(id);
            List<int> viewedStoryIds = new();
            List<int> likedStoryIds = new();
            var currentUserForStories = await _userManager.GetUserAsync(User);
            if (currentUserForStories != null)
            {
                var currentCustomerForStories = await _customerManager.GetCustomerByUserIdAsync(currentUserForStories.Id);
                if (currentCustomerForStories != null)
                {
                    viewedStoryIds = await _storyManager.GetViewedStoryIdsAsync(currentCustomerForStories.CustomerID);
                    foreach (var s in storeStories)
                    {
                        if (await _storyManager.IsLikedByCustomerAsync(s.StoryID, currentCustomerForStories.CustomerID))
                            likedStoryIds.Add(s.StoryID);
                    }
                }
            }

            // storeStories is already ordered oldest -> newest by the repository - correct playback order, keep it
            StoreStories = storeStories
                .Select(s => new StoryDTO
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
                .ToList();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
                if (customer != null)
                {
                    IsFollowing = await _storeManager.IsFollowingAsync(customer.CustomerID, id);
                }

                IsBlocked = await _context.BlockRelations.AnyAsync(b =>
                    b.BlockerUserId == user.Id &&
                    b.BlockedUserId == store.OwnerUserID);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostFollowAsync(int storeId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            await _storeManager.FollowStoreAsync(customer.CustomerID, storeId);
            TempData["Success"] = "Store followed successfully.";
            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostUnfollowAsync(int storeId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            await _storeManager.UnfollowStoreAsync(customer.CustomerID, storeId);
            TempData["Success"] = "Store unfollowed.";
            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostBlockStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToLogin();

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.StoreID == storeId);
            if (store == null)
                return NotFound();

            if (store.OwnerUserID == user.Id)
            {
                TempData["Error"] = "You cannot block your own store.";
                return RedirectToPage(new { id = storeId });
            }

            var existing = await _context.BlockRelations.FirstOrDefaultAsync(b =>
                b.BlockerUserId == user.Id &&
                b.BlockedUserId == store.OwnerUserID);

            if (existing == null)
            {
                _context.BlockRelations.Add(new BlockRelation
                {
                    BlockerUserId = user.Id,
                    BlockedUserId = store.OwnerUserID,
                    BlockerRole = "Customer",
                    BlockedRole = "StoreOwner",
                    CreatedAt = DateTime.UtcNow
                });

                var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
                if (customer != null)
                {
                    var follow = await _context.StoreFollows.FirstOrDefaultAsync(f =>
                        f.CustomerID == customer.CustomerID && f.StoreID == storeId);
                    if (follow != null)
                        _context.StoreFollows.Remove(follow);
                }

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Store blocked.";
            return RedirectToPage("/Customer1");
        }

        public async Task<IActionResult> OnPostUnblockStoreAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToLogin();

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.StoreID == storeId);
            if (store == null)
                return NotFound();

            var existing = await _context.BlockRelations.FirstOrDefaultAsync(b =>
                b.BlockerUserId == user.Id && b.BlockedUserId == store.OwnerUserID);

            if (existing != null)
            {
                _context.BlockRelations.Remove(existing);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Store unblocked.";
            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostReportStoreAsync(
            int storeId,
            string? complaintType,
            string? description)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            var storeExists = await _context.Stores.AnyAsync(s => s.StoreID == storeId);
            if (!storeExists)
                return NotFound();

            var cleanType = string.IsNullOrWhiteSpace(complaintType)
                ? "Store report"
                : complaintType.Trim();
            var cleanDescription = description?.Trim();

            if (string.IsNullOrWhiteSpace(cleanDescription))
            {
                TempData["Error"] = "Please explain why you are reporting this store.";
                return RedirectToPage(new { id = storeId });
            }

            _context.Complaints.Add(new Complaint
            {
                CustomerID = customer.CustomerID,
                StoreID = storeId,
                ProductID = null,
                OrderID = null,
                ComplaintType = cleanType,
                Description = cleanDescription,
                Status = "Pending Review",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Your report was submitted for review.";
            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostReportProductAsync(
            int productId,
            string? description)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null)
                return NotFound();

            var cleanDescription = description?.Trim();
            if (string.IsNullOrWhiteSpace(cleanDescription))
            {
                TempData["Error"] = "Please explain why you are reporting this product.";
                return RedirectToPage(new { id = product.StoreID });
            }

            _context.Complaints.Add(new Complaint
            {
                CustomerID = customer.CustomerID,
                StoreID = product.StoreID,
                ProductID = product.ProductID,
                ComplaintType = "Product report",
                Description = cleanDescription,
                Status = "Pending Review",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Product report submitted.";
            return RedirectToPage(new { id = product.StoreID });
        }

        public async Task<IActionResult> OnPostAddReviewAsync(int productId, int rating, string comment)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            var product = await _context.Products.FirstOrDefaultAsync(x => x.ProductID == productId);
            if (product == null)
                return NotFound();

            rating = Math.Clamp(rating, 1, 5);
            var cleanComment = comment?.Trim();
            if (string.IsNullOrWhiteSpace(cleanComment))
            {
                TempData["Error"] = "Please write a review.";
                return RedirectToPage(new { id = product.StoreID });
            }

            _context.Reviews.Add(new Review
            {
                CustomerID = customer.CustomerID,
                ProductID = productId,
                StoreID = product.StoreID,
                Rating = rating,
                Comment = cleanComment,
                Status = "Approved",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Review posted.";
            return RedirectToPage(new { id = product.StoreID });
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);
            if (product == null)
                return NotFound();

            if (!await _wishlistManager.IsInWishlistAsync(customer.CustomerID, productId))
                await _wishlistManager.AddToWishlistAsync(customer.CustomerID, productId);

            TempData["Success"] = "Product saved to wishlist.";
            return RedirectToPage(new { id = product.StoreID });
        }

        public async Task<IActionResult> OnPostShareToStoreAsync(int productId, int storeOwnerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToLogin();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductID == productId);
            if (product == null)
                return NotFound();

            await _messagingManager.SendProductAsync(user.Id, storeOwnerId, productId);
            TempData["Success"] = "Product shared in chat.";
            return RedirectToPage(new { id = product.StoreID });
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return RedirectToLogin();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductID == productId && p.IsActive);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToPage();
            }

            if (product.Quantity <= 0)
            {
                TempData["Error"] = "This product is out of stock.";
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

            var existingItem = await _context.CartItems.FirstOrDefaultAsync(ci =>
                ci.CartID == cart.CartID && ci.ProductID == productId);

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
            }
            else if (existingItem.Quantity < product.Quantity)
            {
                existingItem.Quantity += 1;
            }
            else
            {
                TempData["Error"] = "You already added the available quantity.";
                return RedirectToPage(new { id = product.StoreID });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Added to cart.";
            return RedirectToPage(new { id = product.StoreID });
        }

        // ================= STORY VIEWED (NEW) =================
        public async Task<IActionResult> OnPostMarkStoryViewedAsync(int storyId)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return new JsonResult(new { success = false });

            await _storyManager.MarkStoryViewedAsync(storyId, customer.CustomerID);
            return new JsonResult(new { success = true });
        }

        // ================= STORY LIKE / UNLIKE (NEW) =================
        public async Task<IActionResult> OnPostToggleStoryLikeAsync(int storyId)
        {
            var customer = await GetCurrentCustomerAsync();
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

            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return new JsonResult(new { success = false, error = "Customer account required." });

            if (string.IsNullOrWhiteSpace(replyText))
                return new JsonResult(new { success = false, error = "Reply cannot be empty." });

            var story = await _storyManager.GetByIdWithStoreAsync(storyId);
            if (story == null)
                return new JsonResult(new { success = false, error = "Story not found." });

            if (story.Store.OwnerUserID == user.Id)
                return new JsonResult(new { success = false, error = "You cannot reply to your own story." });

            await _messagingManager.SendStoryReplyAsync(user.Id, story.Store.OwnerUserID, storyId, replyText);

            return new JsonResult(new { success = true });
        }

        private async Task<CustomerDTO?> GetCurrentCustomerAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user == null
                ? null
                : await _customerManager.GetCustomerByUserIdAsync(user.Id);
        }

        private IActionResult RedirectToLogin() =>
            RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}