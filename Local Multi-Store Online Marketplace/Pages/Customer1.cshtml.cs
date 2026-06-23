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
        private const int ExplorePageSize = 18;

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
                category: SelectedCategory);

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
            string? category = null)
        {
            if (page < 1)
                page = 1;

            var result = await LoadExplorePageAsync(
                page,
                NormalizeCategory(category));

            return new JsonResult(new
            {
                success = true,
                items = result.Items,
                hasMore = result.HasMore,
                page
            });
        }

        // =====================================================
        // OPEN ONE EXPLORE POST IN THE MODAL
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
                    "Explore post was not found.",
                    StatusCodes.Status404NotFound);
            }

            post.ViewCount += 1;
            await _context.SaveChangesAsync();

            var isFollowing = await _context.StoreFollows
                .AnyAsync(f =>
                    f.CustomerID == customerId.Value &&
                    f.StoreID == post.StoreID);

            var details = new ExplorePostDetailsViewModel
            {
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

                ProductID = post.Product != null && post.Product.IsActive
                    ? post.Product.ProductID
                    : null,

                ProductName = post.Product != null && post.Product.IsActive
                    ? post.Product.ProductName
                    : null,

                ProductDescription = post.Product != null && post.Product.IsActive
                    ? post.Product.Description
                    : null,

                ProductPrice = post.Product != null && post.Product.IsActive
                    ? post.Product.Price
                    : null,

                ProductQuantity = post.Product != null && post.Product.IsActive
                    ? post.Product.Quantity
                    : null,

                ProductImageUrl = post.Product != null && post.Product.IsActive
                    ? post.Product.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault() ?? "/images/no-image.png"
                    : null,

                CategoryID = post.Product != null && post.Product.IsActive
                    ? post.Product.CategoryID
                    : null,

                CategoryName = post.Product != null &&
                               post.Product.IsActive &&
                               post.Product.Category != null
                    ? post.Product.Category.CategoryName
                    : null,

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
                    .ToList()
            };

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
        // ADD LINKED PRODUCT TO WISHLIST FROM MODAL
        // =====================================================
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

            var exists = await _wishlistManager
                .IsInWishlistAsync(customerId.Value, productId);

            if (exists)
            {
                return JsonError(
                    "This product is already in your wishlist.",
                    StatusCodes.Status409Conflict);
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
            int page,
            string? category)
        {
            var normalizedCategory = NormalizeCategory(category);

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
                        .FirstOrDefault() ?? "/images/no-image.png",
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
                    product.Images.Any() &&
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
                        .FirstOrDefault() ?? "/images/no-image.png",
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
                        .FirstOrDefault() ?? "/images/no-image.png",
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
                    product.Images.Any() &&
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
                        .FirstOrDefault() ?? "/images/no-image.png",
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
            = "/images/no-image.png";

        public string? ThumbnailUrl { get; set; }

        public int MediaCount { get; set; }

        public string? ProductName { get; set; }

        public decimal? ProductPrice { get; set; }

        public string? CategoryName { get; set; }

        public DateTime SortDate { get; set; }
    }

    public class ExplorePostDetailsViewModel
    {
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

        public List<ExploreMediaViewModel> Media { get; set; }
            = new();

        public List<ExploreCommentViewModel> Comments { get; set; }
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
