using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class Customer1Model : PageModel
    {
        private const int ExplorePageSize = 10;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly WishlistManager _wishlistManager;
        private readonly MessagingManager _messagingManager;

        public Customer1Model(
            ApplicationDbContext context,
            UserManager<User> userManager,
            WishlistManager wishlistManager,
            MessagingManager messagingManager)
        {
            _context = context;
            _userManager = userManager;
            _wishlistManager = wishlistManager;
            _messagingManager = messagingManager;
        }

        public List<ExploreGridItemViewModel> InitialItems { get; set; } = new();

        public List<string> NavbarCategories { get; set; } = new();

        public string? SelectedCategory { get; set; }

        public bool HasMoreItems { get; set; }

        public List<ExploreCategoryFilterViewModel> FilterCategories { get; set; } = new();
        public List<ExploreStoreFilterViewModel> FilterStores { get; set; } = new();
        public List<string> FilterAreas { get; set; } = new();

        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
        [BindProperty(SupportsGet = true)] public int? StoreId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Area { get; set; }
        [BindProperty(SupportsGet = true)] public decimal? MinPrice { get; set; }
        [BindProperty(SupportsGet = true)] public decimal? MaxPrice { get; set; }

        // =====================================================
        // INITIAL PAGE
        // =====================================================
        public async Task<IActionResult> OnGetAsync(string? category)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            SelectedCategory = NormalizeCategory(category);
            await LoadExploreFilterOptionsAsync();

            NavbarCategories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .Select(c => c.CategoryName)
                .Distinct()
                .ToListAsync();

            var pageResult = await LoadExplorePageAsync(
                page: 1,
                category: SelectedCategory,
                searchTerm: SearchTerm,
                categoryId: CategoryId,
                storeId: StoreId,
                area: Area,
                minPrice: MinPrice,
                maxPrice: MaxPrice);

            InitialItems = pageResult.Items;
            HasMoreItems = pageResult.HasMore;

            return Page();
        }

        // =====================================================
        // INFINITE SCROLL PAGE
        // GET /Customer1?handler=ExplorePage&page=2&category=Fashion
        // =====================================================
        public async Task<IActionResult> OnGetExplorePageAsync(
            int page = 1,
            string? category = null,
            string? searchTerm = null,
            int? categoryId = null,
            int? storeId = null,
            string? area = null,
            decimal? minPrice = null,
            decimal? maxPrice = null)
        {
            if (page < 1) page = 1;

            var result = await LoadExplorePageAsync(
                page, NormalizeCategory(category), searchTerm, categoryId,
                storeId, area, minPrice, maxPrice);

            return new JsonResult(new
            {
                success = true,
                items = result.Items,
                hasMore = result.HasMore,
                page
            });
        }

        // =====================================================
        // OPEN ONE EXISTING EXPLORE POST IN THE SHARED MODAL
        // Existing post likes/comments remain unchanged.
        // =====================================================
        public async Task<IActionResult> OnGetExplorePostDetailsAsync(int id)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var post = await _context.ExplorePosts
                .Include(p => p.Store)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Images)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Category)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Reviews)
                        .ThenInclude(r => r.Customer)
                            .ThenInclude(c => c.User)
                .Include(p => p.Media)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Customer)
                        .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p =>
                    p.ExplorePostID == id &&
                    p.IsActive &&
                    p.Store != null &&
                    p.Store.Status == "Approved");

            if (post == null)
            {
                return JsonError(
                    "Explore item was not found.",
                    StatusCodes.Status404NotFound);
            }

            post.ViewCount += 1;
            await _context.SaveChangesAsync();

            var isFollowing = await _context.StoreFollows
                .AnyAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == post.StoreID);

            var hasAvailableProduct =
                post.Product != null &&
                post.Product.IsActive;

            var isInWishlist = hasAvailableProduct &&
                await _wishlistManager.IsInWishlistAsync(
                    customerId.Value,
                    post.Product!.ProductID);

            var details = new ExplorePostDetailsViewModel
            {
                ContentType = "Post",
                ExplorePostID = post.ExplorePostID,
                StoreID = post.StoreID,
                StoreOwnerUserID = post.Store.OwnerUserID,
                StoreName = post.Store.StoreName,
                StoreLogoUrl = post.Store.LogoURL,
                IsFollowingStore = isFollowing,

                PostType = post.PostType,
                Caption = post.Caption,
                IsFeatured = post.IsFeatured,
                ViewCount = post.ViewCount,
                CreatedAt = post.CreatedAt,

                LikeCount = post.Likes.Count,
                CommentCount = post.Comments.Count(c => !c.IsDeleted),
                IsLikedByCurrentCustomer = post.Likes
                    .Any(l => l.CustomerID == customerId.Value),

                ProductID = hasAvailableProduct
                    ? post.Product!.ProductID
                    : null,

                ProductName = hasAvailableProduct
                    ? post.Product!.ProductName
                    : null,

                ProductDescription = hasAvailableProduct
                    ? post.Product!.Description
                    : null,

                ProductPrice = hasAvailableProduct
                    ? post.Product!.Price
                    : null,

                ProductQuantity = hasAvailableProduct
                    ? post.Product!.Quantity
                    : null,

                ProductImageUrl = hasAvailableProduct
                    ? post.Product!.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/product-placeholder.svg"
                    : null,

                CategoryID = hasAvailableProduct
                    ? post.Product!.CategoryID
                    : null,

                CategoryName = hasAvailableProduct &&
                               post.Product!.Category != null
                    ? post.Product.Category.CategoryName
                    : null,

                ProductRating = hasAvailableProduct
                    ? post.Product!.Rating
                    : 0,

                ProductTotalRatings = hasAvailableProduct
                    ? post.Product!.TotalRatings
                    : 0,

                IsInWishlist = isInWishlist,

                Media = post.Media
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => new ExploreMediaViewModel
                    {
                        ExploreMediaID = m.ExploreMediaID,
                        MediaType = m.MediaType,
                        MediaUrl = m.MediaUrl,
                        ThumbnailUrl = m.ThumbnailUrl,
                        DisplayOrder = m.DisplayOrder,
                        DurationSeconds = m.DurationSeconds
                    })
                    .ToList(),

                Comments = post.Comments
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new ExploreCommentViewModel
                    {
                        ExploreCommentID = c.ExploreCommentID,
                        CustomerID = c.CustomerID,
                        CustomerName = GetCustomerDisplayName(c.Customer),
                        CommentText = c.CommentText,
                        CreatedAt = c.CreatedAt,
                        CanDelete = c.CustomerID == customerId.Value
                    })
                    .ToList(),

                Reviews = MapProductReviews(post.Product)
            };

            if (details.Media.Count == 0)
            {
                details.Media.Add(new ExploreMediaViewModel
                {
                    MediaType = "Image",
                    MediaUrl = details.ProductImageUrl ??
                        "/images/product-placeholder.svg",
                    DisplayOrder = 0
                });
            }

            details.RelatedItems = await LoadRelatedItemsAsync(
                currentPostId: post.ExplorePostID,
                currentProductId: post.ProductID,
                storeId: post.StoreID,
                categoryId: details.CategoryID);

            return new JsonResult(new
            {
                success = true,
                post = details
            });
        }

        // =====================================================
        // OPEN A NORMAL PRODUCT IN THE SAME SHEIN-STYLE MODAL
        // A store owner only creates a normal product. It appears
        // automatically in Customer Explore without another post.
        // =====================================================
        public async Task<IActionResult> OnGetExploreProductDetailsAsync(int id)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var product = await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.Customer)
                        .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p =>
                    p.ProductID == id &&
                    p.IsActive &&
                    p.Store != null &&
                    p.Store.Status == "Approved");

            if (product == null)
            {
                return JsonError(
                    "Product was not found.",
                    StatusCodes.Status404NotFound);
            }

            await SaveRecentlyViewedAsync(
                customerId.Value,
                product.ProductID);

            var isFollowing = await _context.StoreFollows
                .AnyAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == product.StoreID);

            var isInWishlist = await _wishlistManager
                .IsInWishlistAsync(
                    customerId.Value,
                    product.ProductID);

            var media = product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder)
                .Select(i => new ExploreMediaViewModel
                {
                    ExploreMediaID = i.ImageID,
                    MediaType = "Image",
                    MediaUrl = i.ImageUrl,
                    DisplayOrder = i.DisplayOrder
                })
                .ToList();

            if (media.Count == 0)
            {
                media.Add(new ExploreMediaViewModel
                {
                    MediaType = "Image",
                    MediaUrl = "/images/product-placeholder.svg",
                    DisplayOrder = 0
                });
            }

            var details = new ExplorePostDetailsViewModel
            {
                ContentType = "Product",
                ExplorePostID = 0,
                StoreID = product.StoreID,
                StoreOwnerUserID = product.Store.OwnerUserID,
                StoreName = product.Store.StoreName,
                StoreLogoUrl = product.Store.LogoURL,
                IsFollowingStore = isFollowing,

                PostType = "Product",
                Caption = product.Description,
                CreatedAt = product.CreatedAt,

                ProductID = product.ProductID,
                ProductName = product.ProductName,
                ProductDescription = product.Description,
                ProductPrice = product.Price,
                ProductQuantity = product.Quantity,
                ProductImageUrl = media.First().MediaUrl,
                CategoryID = product.CategoryID,
                CategoryName = product.Category?.CategoryName,
                ProductRating = product.Rating,
                ProductTotalRatings = product.TotalRatings,
                IsInWishlist = isInWishlist,

                Media = media,
                Reviews = MapProductReviews(product)
            };

            details.RelatedItems = await LoadRelatedItemsAsync(
                currentPostId: 0,
                currentProductId: product.ProductID,
                storeId: product.StoreID,
                categoryId: product.CategoryID);

            return new JsonResult(new
            {
                success = true,
                product = details
            });
        }

        // =====================================================
        // LIKE / UNLIKE EXPLORE POST
        // =====================================================
        public async Task<IActionResult> OnPostToggleExploreLikeAsync(int postId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var postExists = await _context.ExplorePosts
                .AnyAsync(p =>
                    p.ExplorePostID == postId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

            if (!postExists)
            {
                return JsonError(
                    "Explore post is no longer available.",
                    StatusCodes.Status404NotFound);
            }

            var existingLike = await _context.ExploreLikes
                .FirstOrDefaultAsync(l =>
                    l.ExplorePostID == postId &&
                    l.CustomerID == customerId.Value);

            bool liked;

            if (existingLike == null)
            {
                _context.ExploreLikes.Add(new ExploreLike
                {
                    ExplorePostID = postId,
                    CustomerID = customerId.Value,
                    CreatedAt = DateTime.UtcNow
                });

                liked = true;
            }
            else
            {
                _context.ExploreLikes.Remove(existingLike);
                liked = false;
            }

            await _context.SaveChangesAsync();

            var likeCount = await _context.ExploreLikes
                .CountAsync(l => l.ExplorePostID == postId);

            return new JsonResult(new
            {
                success = true,
                liked,
                likeCount
            });
        }

        // =====================================================
        // ADD EXPLORE COMMENT
        // =====================================================
        public async Task<IActionResult> OnPostAddExploreCommentAsync(
            int postId,
            string? commentText)
        {
            var customer = await GetCurrentCustomerAsync();

            if (customer == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var cleanComment = commentText?.Trim();

            if (string.IsNullOrWhiteSpace(cleanComment))
            {
                return JsonError(
                    "Please write a comment.",
                    StatusCodes.Status400BadRequest);
            }

            if (cleanComment.Length > 1000)
            {
                return JsonError(
                    "Comment cannot exceed 1000 characters.",
                    StatusCodes.Status400BadRequest);
            }

            var postExists = await _context.ExplorePosts
                .AnyAsync(p =>
                    p.ExplorePostID == postId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

            if (!postExists)
            {
                return JsonError(
                    "Explore post is no longer available.",
                    StatusCodes.Status404NotFound);
            }

            var comment = new ExploreComment
            {
                ExplorePostID = postId,
                CustomerID = customer.CustomerID,
                CommentText = cleanComment,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            _context.ExploreComments.Add(comment);
            await _context.SaveChangesAsync();

            var commentCount = await _context.ExploreComments
                .CountAsync(c =>
                    c.ExplorePostID == postId &&
                    !c.IsDeleted);

            return new JsonResult(new
            {
                success = true,
                message = "Comment added successfully.",
                commentCount,
                comment = new ExploreCommentViewModel
                {
                    ExploreCommentID = comment.ExploreCommentID,
                    CustomerID = customer.CustomerID,
                    CustomerName = GetCustomerDisplayName(customer),
                    CommentText = comment.CommentText,
                    CreatedAt = comment.CreatedAt,
                    CanDelete = true
                }
            });
        }

        // =====================================================
        // DELETE OWN EXPLORE COMMENT
        // =====================================================
        public async Task<IActionResult> OnPostDeleteExploreCommentAsync(
            int postId,
            int commentId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var comment = await _context.ExploreComments
                .FirstOrDefaultAsync(c =>
                    c.ExploreCommentID == commentId &&
                    c.ExplorePostID == postId &&
                    c.CustomerID == customerId.Value &&
                    !c.IsDeleted);

            if (comment == null)
            {
                return JsonError(
                    "Comment was not found.",
                    StatusCodes.Status404NotFound);
            }

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var commentCount = await _context.ExploreComments
                .CountAsync(c =>
                    c.ExplorePostID == postId &&
                    !c.IsDeleted);

            return new JsonResult(new
            {
                success = true,
                message = "Comment deleted.",
                commentCount
            });
        }

        // =====================================================
        // FOLLOW / UNFOLLOW STORE FROM MODAL
        // =====================================================
        public async Task<IActionResult> OnPostToggleExploreStoreFollowAsync(
            int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var storeExists = await _context.Stores
                .AnyAsync(s =>
                    s.StoreID == storeId &&
                    s.Status == "Approved");

            if (!storeExists)
            {
                return JsonError(
                    "Store was not found.",
                    StatusCodes.Status404NotFound);
            }

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == storeId);

            bool following;

            if (follow == null)
            {
                _context.StoreFollows.Add(new StoreFollow
                {
                    CustomerID = customerId.Value,
                    StoreID = storeId,
                    FollowedAt = DateTime.UtcNow
                });

                following = true;
            }
            else
            {
                _context.StoreFollows.Remove(follow);
                following = false;
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                following,
                message = following
                    ? "Store followed successfully."
                    : "Store unfollowed successfully."
            });
        }

        // =====================================================
        // SAVE / UNSAVE PRODUCT FROM THE SHARED EXPLORE MODAL
        // =====================================================
        public async Task<IActionResult> OnPostToggleExploreWishlistAsync(
            int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

            if (product == null)
            {
                return JsonError(
                    "Product is not available.",
                    StatusCodes.Status404NotFound);
            }

            var saved = await _wishlistManager
                .IsInWishlistAsync(
                    customerId.Value,
                    productId);

            if (saved)
            {
                await _wishlistManager.RemoveFromWishlistAsync(
                    customerId.Value,
                    productId);

                saved = false;
            }
            else
            {
                await _wishlistManager.AddToWishlistAsync(
                    customerId.Value,
                    productId);

                saved = true;
            }

            return new JsonResult(new
            {
                success = true,
                saved,
                message = saved
                    ? $"{product.ProductName} saved to your wishlist."
                    : $"{product.ProductName} removed from your wishlist."
            });
        }

        // Existing handler kept so old links/forms do not break.
        public async Task<IActionResult> OnPostExploreAddWishlistAsync(
            int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

            if (product == null)
            {
                return JsonError(
                    "Product is not available.",
                    StatusCodes.Status404NotFound);
            }

            await _wishlistManager.AddToWishlistAsync(
                customerId.Value,
                productId);

            return new JsonResult(new
            {
                success = true,
                message = $"{product.ProductName} added to your wishlist."
            });
        }

        // =====================================================
        // ADD LINKED PRODUCT TO CART FROM MODAL
        // =====================================================
        public async Task<IActionResult> OnPostExploreAddToCartAsync(
            int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                return JsonError(
                    "Please login as a customer first.",
                    StatusCodes.Status401Unauthorized);
            }

            var result = await AddProductToCartInternalAsync(
                customerId.Value,
                productId);

            if (!result.Success)
            {
                return JsonError(
                    result.Message,
                    result.StatusCode);
            }

            return new JsonResult(new
            {
                success = true,
                message = result.Message
            });
        }

        // =====================================================
        // EXISTING STANDARD HANDLERS KEPT FOR COMPATIBILITY
        // =====================================================
        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var result = await AddProductToCartInternalAsync(
                customerId.Value,
                productId);

            TempData[result.Success ? "Success" : "Error"] = result.Message;

            return RedirectToPage(new { category = SelectedCategory });
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product is not available.";
                return RedirectToPage();
            }

            if (await _wishlistManager.IsInWishlistAsync(
                    customerId.Value,
                    productId))
            {
                TempData["Error"] =
                    "This product is already in your wishlist.";

                return RedirectToPage();
            }

            await _wishlistManager.AddToWishlistAsync(
                customerId.Value,
                productId);

            TempData["Success"] =
                $"{product.ProductName} added to your wishlist.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostFollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var exists = await _context.StoreFollows
                .AnyAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == storeId);

            if (!exists)
            {
                _context.StoreFollows.Add(new StoreFollow
                {
                    CustomerID = customerId.Value,
                    StoreID = storeId,
                    FollowedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnfollowStoreAsync(int storeId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var follow = await _context.StoreFollows
                .FirstOrDefaultAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == storeId);

            if (follow != null)
            {
                _context.StoreFollows.Remove(follow);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetShareToStoreAsync(
            int productId,
            int storeOwnerId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var productExists = await _context.Products
                .AnyAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive);

            var receiverExists = await _context.Users
                .AnyAsync(u => u.Id == storeOwnerId);

            if (!productExists || !receiverExists)
            {
                TempData["Error"] =
                    "Product or store owner was not found.";

                return RedirectToPage();
            }

            await _messagingManager.SendProductAsync(
                user.Id,
                storeOwnerId,
                productId);

            TempData["Success"] =
                "Product shared successfully.";

            return RedirectToPage();
        }

        // =====================================================
        // PAGINATED UNIFIED GRID:
        // Explore posts + products that do not already have an active post
        // =====================================================
        private async Task<ExplorePageResult> LoadExplorePageAsync(
            int page, string? category, string? searchTerm, int? categoryId,
            int? storeId, string? area, decimal? minPrice, decimal? maxPrice)
        {
            var normalizedCategory = NormalizeCategory(category);
            var normalizedSearch = searchTerm?.Trim();
            var normalizedArea = area?.Trim();

            var postsQuery = _context.ExplorePosts
                .AsNoTracking()
                .Where(post =>
                    post.IsActive &&
                    post.Store != null &&
                    post.Store.Status == "Approved" &&
                    post.Media.Any());

            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                postsQuery = postsQuery.Where(post =>
                    post.Product != null &&
                    post.Product.IsActive &&
                    post.Product.Category != null &&
                    post.Product.Category.CategoryName.ToLower() ==
                        normalizedCategory.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
                postsQuery = postsQuery.Where(post =>
                    post.Caption.Contains(normalizedSearch) ||
                    post.Store.StoreName.Contains(normalizedSearch) ||
                    (post.Product != null && (
                        post.Product.ProductName.Contains(normalizedSearch) ||
                        post.Product.Description.Contains(normalizedSearch) ||
                        (post.Product.Category != null && post.Product.Category.CategoryName.Contains(normalizedSearch)))));

            if (categoryId.HasValue && categoryId.Value > 0)
                postsQuery = postsQuery.Where(post => post.Product != null && post.Product.CategoryID == categoryId.Value);
            if (storeId.HasValue && storeId.Value > 0)
                postsQuery = postsQuery.Where(post => post.StoreID == storeId.Value);
            if (!string.IsNullOrWhiteSpace(normalizedArea))
                postsQuery = postsQuery.Where(post => post.Store.Area == normalizedArea);
            if (minPrice.HasValue)
                postsQuery = postsQuery.Where(post => post.Product != null && post.Product.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                postsQuery = postsQuery.Where(post => post.Product != null && post.Product.Price <= maxPrice.Value);

            var postItems = await postsQuery
                .Select(post => new ExploreGridItemViewModel
                {
                    GridItemType = "Post",
                    ExplorePostID = post.ExplorePostID,
                    ProductID = post.ProductID,
                    StoreID = post.StoreID,
                    StoreName = post.Store.StoreName,
                    StoreLogoUrl = post.Store.LogoURL,
                    PostType = post.PostType,
                    MediaType = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.MediaType)
                        .FirstOrDefault() ?? "Image",
                    MediaUrl = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.MediaUrl)
                        .FirstOrDefault() ?? "/images/product-placeholder.svg",
                    ThumbnailUrl = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.ThumbnailUrl)
                        .FirstOrDefault(),
                    MediaCount = post.Media.Count,
                    ProductName = post.Product != null &&
                                  post.Product.IsActive
                        ? post.Product.ProductName
                        : null,
                    ProductPrice = post.Product != null &&
                                   post.Product.IsActive
                        ? post.Product.Price
                        : null,
                    CategoryName = post.Product != null &&
                                   post.Product.IsActive &&
                                   post.Product.Category != null
                        ? post.Product.Category.CategoryName
                        : null,
                    SortDate = post.CreatedAt
                })
                .ToListAsync();

            var productsQuery = _context.Products
                .AsNoTracking()
                .Where(product =>
                    product.IsActive &&
                    product.Store != null &&
                    product.Store.Status == "Approved" &&
                    !product.ExplorePosts.Any(post =>
                        post.IsActive &&
                        post.Media.Any()));

            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                productsQuery = productsQuery.Where(product =>
                    product.Category != null &&
                    product.Category.CategoryName.ToLower() ==
                        normalizedCategory.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
                productsQuery = productsQuery.Where(product =>
                    product.ProductName.Contains(normalizedSearch) ||
                    product.Description.Contains(normalizedSearch) ||
                    product.Store.StoreName.Contains(normalizedSearch) ||
                    (product.Category != null && product.Category.CategoryName.Contains(normalizedSearch)));

            if (categoryId.HasValue && categoryId.Value > 0)
                productsQuery = productsQuery.Where(product => product.CategoryID == categoryId.Value);
            if (storeId.HasValue && storeId.Value > 0)
                productsQuery = productsQuery.Where(product => product.StoreID == storeId.Value);
            if (!string.IsNullOrWhiteSpace(normalizedArea))
                productsQuery = productsQuery.Where(product => product.Store.Area == normalizedArea);
            if (minPrice.HasValue)
                productsQuery = productsQuery.Where(product => product.Price >= minPrice.Value);
            if (maxPrice.HasValue)
                productsQuery = productsQuery.Where(product => product.Price <= maxPrice.Value);

            var productItems = await productsQuery
                .Select(product => new ExploreGridItemViewModel
                {
                    GridItemType = "Product",
                    ExplorePostID = null,
                    ProductID = product.ProductID,
                    StoreID = product.StoreID,
                    StoreName = product.Store.StoreName,
                    StoreLogoUrl = product.Store.LogoURL,
                    PostType = "Product",
                    MediaType = "Image",
                    MediaUrl = product.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/product-placeholder.svg",
                    ThumbnailUrl = null,
                    MediaCount = product.Images.Count,
                    ProductName = product.ProductName,
                    ProductPrice = product.Price,
                    CategoryName = product.Category != null
                        ? product.Category.CategoryName
                        : null,
                    SortDate = product.CreatedAt
                })
                .ToListAsync();

            var skip = (page - 1) * ExplorePageSize;

            var pageItems = postItems
                .Concat(productItems)
                .OrderByDescending(item => item.SortDate)
                .ThenByDescending(item => item.ExplorePostID ?? 0)
                .ThenByDescending(item => item.ProductID ?? 0)
                .Skip(skip)
                .Take(ExplorePageSize + 1)
                .ToList();

            var hasMore = pageItems.Count > ExplorePageSize;

            if (hasMore)
            {
                pageItems.RemoveAt(pageItems.Count - 1);
            }

            return new ExplorePageResult
            {
                Items = pageItems,
                HasMore = hasMore
            };
        }

        // =====================================================
        // RELATED POSTS / PRODUCTS INSIDE MODAL
        // =====================================================
        private async Task<List<ExploreGridItemViewModel>>
            LoadRelatedItemsAsync(
                int currentPostId,
                int? currentProductId,
                int storeId,
                int? categoryId)
        {
            var relatedPostsQuery = _context.ExplorePosts
                .AsNoTracking()
                .Where(post =>
                    post.ExplorePostID != currentPostId &&
                    post.IsActive &&
                    post.Store.Status == "Approved" &&
                    post.Media.Any() &&
                    (
                        post.StoreID == storeId ||
                        (
                            categoryId.HasValue &&
                            post.Product != null &&
                            post.Product.IsActive &&
                            post.Product.CategoryID == categoryId.Value
                        )
                    ));

            var relatedPosts = await relatedPostsQuery
                .OrderByDescending(post =>
                    categoryId.HasValue &&
                    post.Product != null &&
                    post.Product.CategoryID == categoryId.Value)
                .ThenByDescending(post => post.CreatedAt)
                .Take(8)
                .Select(post => new ExploreGridItemViewModel
                {
                    GridItemType = "Post",
                    ExplorePostID = post.ExplorePostID,
                    ProductID = post.ProductID,
                    StoreID = post.StoreID,
                    StoreName = post.Store.StoreName,
                    StoreLogoUrl = post.Store.LogoURL,
                    PostType = post.PostType,
                    MediaType = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.MediaType)
                        .FirstOrDefault() ?? "Image",
                    MediaUrl = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.MediaUrl)
                        .FirstOrDefault() ?? "/images/product-placeholder.svg",
                    ThumbnailUrl = post.Media
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.ThumbnailUrl)
                        .FirstOrDefault(),
                    MediaCount = post.Media.Count,
                    ProductName = post.Product != null &&
                                  post.Product.IsActive
                        ? post.Product.ProductName
                        : null,
                    ProductPrice = post.Product != null &&
                                   post.Product.IsActive
                        ? post.Product.Price
                        : null,
                    CategoryName = post.Product != null &&
                                   post.Product.Category != null
                        ? post.Product.Category.CategoryName
                        : null,
                    SortDate = post.CreatedAt
                })
                .ToListAsync();

            var relatedProductsQuery = _context.Products
                .AsNoTracking()
                .Where(product =>
                    product.IsActive &&
                    product.Store.Status == "Approved" &&
                    !product.ExplorePosts.Any(post =>
                        post.IsActive &&
                        post.Media.Any()) &&
                    (!currentProductId.HasValue ||
                     product.ProductID != currentProductId.Value) &&
                    (
                        product.StoreID == storeId ||
                        (
                            categoryId.HasValue &&
                            product.CategoryID == categoryId.Value
                        )
                    ));

            var relatedProducts = await relatedProductsQuery
                .OrderByDescending(product =>
                    categoryId.HasValue &&
                    product.CategoryID == categoryId.Value)
                .ThenByDescending(product => product.Rating)
                .ThenByDescending(product => product.CreatedAt)
                .Take(8)
                .Select(product => new ExploreGridItemViewModel
                {
                    GridItemType = "Product",
                    ExplorePostID = null,
                    ProductID = product.ProductID,
                    StoreID = product.StoreID,
                    StoreName = product.Store.StoreName,
                    StoreLogoUrl = product.Store.LogoURL,
                    PostType = "Product",
                    MediaType = "Image",
                    MediaUrl = product.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/product-placeholder.svg",
                    ThumbnailUrl = null,
                    MediaCount = product.Images.Count,
                    ProductName = product.ProductName,
                    ProductPrice = product.Price,
                    CategoryName = product.Category.CategoryName,
                    SortDate = product.CreatedAt
                })
                .ToListAsync();

            return relatedPosts
                .Concat(relatedProducts)
                .OrderByDescending(item => item.SortDate)
                .Take(12)
                .ToList();
        }

        // =====================================================
        // CART HELPER
        // =====================================================
        private async Task<CartActionResult>
            AddProductToCartInternalAsync(
                int customerId,
                int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.ProductID == productId &&
                    p.IsActive &&
                    p.Store.Status == "Approved");

            if (product == null)
            {
                return CartActionResult.Fail(
                    "Product is not available.",
                    StatusCodes.Status404NotFound);
            }

            if (product.Quantity <= 0)
            {
                return CartActionResult.Fail(
                    "This product is out of stock.",
                    StatusCodes.Status409Conflict);
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerID = customerId,
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

            if (existingItem != null)
            {
                return CartActionResult.Fail(
                    "This product is already in your cart. " +
                    "You can update the quantity from the cart page.",
                    StatusCodes.Status409Conflict);
            }

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

            return CartActionResult.Ok(
                $"{product.ProductName} added to your cart.");
        }

        // =====================================================
        // PRODUCT REVIEWS / RECENTLY VIEWED HELPERS
        // =====================================================
        private static List<ExploreProductReviewViewModel>
            MapProductReviews(Product? product)
        {
            if (product == null)
                return new List<ExploreProductReviewViewModel>();

            return product.Reviews
                .Where(r => !string.Equals(
                    r.Status,
                    "Rejected",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .Select(r => new ExploreProductReviewViewModel
                {
                    ReviewID = r.ReviewID,
                    CustomerName = GetCustomerDisplayName(r.Customer),
                    Rating = r.Rating,
                    Comment = r.Comment,
                    IsVerifiedPurchase = r.IsVerifiedPurchase,
                    CreatedAt = r.CreatedAt
                })
                .ToList();
        }

        private async Task SaveRecentlyViewedAsync(
            int customerId,
            int productId)
        {
            var existing = await _context.RecentlyViewedProducts
                .FirstOrDefaultAsync(item =>
                    item.CustomerID == customerId &&
                    item.ProductID == productId);

            if (existing == null)
            {
                _context.RecentlyViewedProducts.Add(
                    new RecentlyViewedProduct
                    {
                        CustomerID = customerId,
                        ProductID = productId,
                        ViewedAt = DateTime.UtcNow
                    });
            }
            else
            {
                existing.ViewedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private async Task LoadExploreFilterOptionsAsync()
        {
            FilterCategories = await _context.Categories.AsNoTracking()
                .Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
                .Select(c => new ExploreCategoryFilterViewModel { CategoryID = c.CategoryID, CategoryName = c.CategoryName })
                .ToListAsync();

            FilterStores = await _context.Stores.AsNoTracking()
                .Where(s => s.Status == "Approved").OrderBy(s => s.StoreName)
                .Select(s => new ExploreStoreFilterViewModel { StoreID = s.StoreID, StoreName = s.StoreName })
                .ToListAsync();

            FilterAreas = await _context.Stores.AsNoTracking()
                .Where(s => s.Status == "Approved" && s.Area != null && s.Area != "")
                .Select(s => s.Area!).Distinct().OrderBy(a => a).ToListAsync();
        }

        // =====================================================
        // CURRENT CUSTOMER
        // =====================================================
        private async Task<Customer?> GetCurrentCustomerAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            return await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserID == user.Id);
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            return await _context.Customers
                .Where(c => c.UserID == user.Id)
                .Select(c => (int?)c.CustomerID)
                .FirstOrDefaultAsync();
        }

        private static string GetCustomerDisplayName(Customer? customer)
        {
            if (customer?.User == null)
                return "Customer";

            if (!string.IsNullOrWhiteSpace(customer.User.FullName))
                return customer.User.FullName;

            if (!string.IsNullOrWhiteSpace(customer.User.UserName))
                return customer.User.UserName;

            return "Customer";
        }

        private static string? NormalizeCategory(string? category)
        {
            if (string.IsNullOrWhiteSpace(category) ||
                string.Equals(
                    category,
                    "All",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return category.Trim();
        }

        private JsonResult JsonError(
            string message,
            int statusCode)
        {
            return new JsonResult(new
            {
                success = false,
                message
            })
            {
                StatusCode = statusCode
            };
        }
    }

    // =========================================================
    // VIEW MODELS
    // =========================================================
    public class ExplorePageResult
    {
        public List<ExploreGridItemViewModel> Items { get; set; }
            = new();

        public bool HasMore { get; set; }
    }

    public class ExploreGridItemViewModel
    {
        public string GridItemType { get; set; } = "Post";

        public int? ExplorePostID { get; set; }

        public int? ProductID { get; set; }

        public int StoreID { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string? StoreLogoUrl { get; set; }

        public string PostType { get; set; } = "Image";

        public string MediaType { get; set; } = "Image";

        public string MediaUrl { get; set; }
            = "/images/product-placeholder.svg";

        public string? ThumbnailUrl { get; set; }

        public int MediaCount { get; set; }

        public string? ProductName { get; set; }

        public decimal? ProductPrice { get; set; }

        public string? CategoryName { get; set; }

        public DateTime SortDate { get; set; }
    }

    public class ExplorePostDetailsViewModel
    {
        public string ContentType { get; set; } = "Post";

        public int ExplorePostID { get; set; }

        public int StoreID { get; set; }

        public int StoreOwnerUserID { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string? StoreLogoUrl { get; set; }

        public bool IsFollowingStore { get; set; }

        public string PostType { get; set; } = "Image";

        public string Caption { get; set; } = string.Empty;

        public bool IsFeatured { get; set; }

        public int ViewCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        public bool IsLikedByCurrentCustomer { get; set; }

        public int? ProductID { get; set; }

        public string? ProductName { get; set; }

        public string? ProductDescription { get; set; }

        public decimal? ProductPrice { get; set; }

        public int? ProductQuantity { get; set; }

        public string? ProductImageUrl { get; set; }

        public int? CategoryID { get; set; }

        public string? CategoryName { get; set; }

        public decimal ProductRating { get; set; }

        public int ProductTotalRatings { get; set; }

        public bool IsInWishlist { get; set; }

        public List<ExploreMediaViewModel> Media { get; set; }
            = new();

        public List<ExploreCommentViewModel> Comments { get; set; }
            = new();

        public List<ExploreProductReviewViewModel> Reviews { get; set; }
            = new();

        public List<ExploreGridItemViewModel> RelatedItems { get; set; }
            = new();

        public bool IsOutOfStock =>
            ProductQuantity.HasValue &&
            ProductQuantity.Value <= 0;
    }

    public class ExploreMediaViewModel
    {
        public int ExploreMediaID { get; set; }

        public string MediaType { get; set; } = "Image";

        public string MediaUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public int DisplayOrder { get; set; }

        public int? DurationSeconds { get; set; }
    }

    public class ExploreCommentViewModel
    {
        public int ExploreCommentID { get; set; }

        public int CustomerID { get; set; }

        public string CustomerName { get; set; } = "Customer";

        public string CommentText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool CanDelete { get; set; }
    }

    public class ExploreProductReviewViewModel
    {
        public int ReviewID { get; set; }

        public string CustomerName { get; set; } = "Customer";

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public bool IsVerifiedPurchase { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ExploreCategoryFilterViewModel
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class ExploreStoreFilterViewModel
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
    }

    public class CartActionResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public int StatusCode { get; private set; }

        public static CartActionResult Ok(string message)
        {
            return new CartActionResult
            {
                Success = true,
                Message = message,
                StatusCode = StatusCodes.Status200OK
            };
        }

        public static CartActionResult Fail(
            string message,
            int statusCode)
        {
            return new CartActionResult
            {
                Success = false,
                Message = message,
                StatusCode = statusCode
            };
        }
    }
}
