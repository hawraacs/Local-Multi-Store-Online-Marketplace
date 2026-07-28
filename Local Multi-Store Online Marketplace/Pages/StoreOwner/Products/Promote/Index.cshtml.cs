using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Promote
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public IndexModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
        }

        public List<ExplorePostListViewModel> Posts { get; set; } = new();

        public string StoreName { get; set; } = string.Empty;

        // NEW — lets the view tell "you've never posted anything" apart from
        // "nothing matches your current filter", so the empty state can show
        // the right message and the right call to action for each case.
        public bool HasAnyPosts { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
            {
                return RedirectToPage("/Account/AccessDenied");
            }

            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] =
                    "Store was not found. Please contact the administrator.";

                return RedirectToPage("/StoreOwner/Dashboard");
            }

            StoreName = store.StoreName;

            ViewData["StoreName"] = store.StoreName;
            ViewData["StoreId"] = store.StoreID;

            var query = _context.ExplorePosts
                .AsNoTracking()
                .Where(post => post.StoreID == store.StoreID)
                .Include(post => post.Media)
                .Include(post => post.Product)
                .Include(post => post.Likes)
                    .ThenInclude(like => like.Customer)
                        .ThenInclude(customer => customer.User)
                .Include(post => post.Comments)
                    .ThenInclude(comment => comment.Customer)
                        .ThenInclude(customer => customer.User)
                .AsQueryable();

            // NEW — checked before any filter is applied, so the empty state
            // can tell "no posts exist yet" apart from "no posts match the
            // current filter" (previously both showed the same "create your
            // first post" message, which was misleading once a filter had
            // simply excluded everything).
            HasAnyPosts = await query.AnyAsync();

            if (!string.IsNullOrWhiteSpace(TypeFilter) &&
                TypeFilter != "All")
            {
                query = query.Where(post =>
                    post.PostType == TypeFilter);
            }

            if (StatusFilter == "Active")
            {
                query = query.Where(post => post.IsActive);
            }
            else if (StatusFilter == "Inactive")
            {
                query = query.Where(post => !post.IsActive);
            }

            var posts = await query
                .OrderByDescending(post => post.CreatedAt)
                .ToListAsync();

            Posts = posts.Select(post =>
            {
                var firstMedia = post.Media
                    .OrderBy(media => media.DisplayOrder)
                    .FirstOrDefault();

                return new ExplorePostListViewModel
                {
                    ExplorePostID = post.ExplorePostID,
                    PostType = post.PostType,
                    Caption = post.Caption,
                    IsActive = post.IsActive,
                    IsFeatured = post.IsFeatured,
                    ViewCount = post.ViewCount,
                    CreatedAt = post.CreatedAt,

                    ProductID = post.ProductID,
                    ProductName = post.Product?.ProductName,
                    ProductPrice = post.Product?.Price,

                    MediaUrl = firstMedia?.MediaUrl
                        ?? "/images/no-image.png",

                    ThumbnailUrl = firstMedia?.ThumbnailUrl,

                    MediaType = firstMedia?.MediaType
                        ?? "Image",

                    MediaCount = post.Media.Count,

                    LikeCount = post.Likes.Count,

                    CommentCount = post.Comments.Count(comment =>
                        !comment.IsDeleted),

                    // NEW — the actual likers and comments, not just counts.
                    Likes = post.Likes
                        .OrderByDescending(like => like.CreatedAt)
                        .Select(like => new PostLikeViewModel
                        {
                            CustomerName = GetCustomerDisplayName(like.Customer),
                            CreatedAt = like.CreatedAt
                        })
                        .ToList(),

                    Comments = post.Comments
                        .Where(comment => !comment.IsDeleted)
                        .OrderByDescending(comment => comment.CreatedAt)
                        .Select(comment => new PostCommentViewModel
                        {
                            CustomerName = GetCustomerDisplayName(comment.Customer),
                            CommentText = comment.CommentText,
                            CreatedAt = comment.CreatedAt
                        })
                        .ToList()
                };
            }).ToList();

            return Page();
        }

        // NEW — same display-name convention used on the customer-facing
        // Explore page: prefer the account's full name, fall back to
        // username, fall back to a generic "Customer" if neither is set.
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

        public async Task<IActionResult> OnPostToggleStatusAsync(int postId)
        {
            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store was not found.";
                return RedirectToPage();
            }

            var post = await _context.ExplorePosts
                .FirstOrDefaultAsync(item =>
                    item.ExplorePostID == postId &&
                    item.StoreID == store.StoreID);

            if (post == null)
            {
                TempData["ErrorMessage"] =
                    "Explore post was not found.";

                return RedirectToPage();
            }

            post.IsActive = !post.IsActive;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = post.IsActive
                ? "Explore post activated successfully."
                : "Explore post deactivated successfully.";

            // CHANGED — preserve whatever filter the owner had active
            // instead of always bouncing them back to the unfiltered list.
            return RedirectToPage(new { TypeFilter, StatusFilter });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int postId)
        {
            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store was not found.";
                return RedirectToPage();
            }

            var post = await _context.ExplorePosts
                .Include(item => item.Media)
                .Include(item => item.Likes)
                .Include(item => item.Comments)
                .FirstOrDefaultAsync(item =>
                    item.ExplorePostID == postId &&
                    item.StoreID == store.StoreID);

            if (post == null)
            {
                TempData["ErrorMessage"] =
                    "Explore post was not found.";

                return RedirectToPage();
            }

            var folderPath = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "explore",
                post.ExplorePostID.ToString());

            _context.ExplorePosts.Remove(post);

            await _context.SaveChangesAsync();

            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch
                {
                    // Database deletion already succeeded.
                    // Physical file cleanup can be handled later.
                }
            }

            TempData["SuccessMessage"] =
                "Explore post deleted successfully.";

            // CHANGED — preserve whatever filter the owner had active.
            return RedirectToPage(new { TypeFilter, StatusFilter });
        }
    }

    public class ExplorePostListViewModel
    {
        public int ExplorePostID { get; set; }

        public string PostType { get; set; } = "Image";

        public string Caption { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public bool IsFeatured { get; set; }

        public int ViewCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? ProductID { get; set; }

        public string? ProductName { get; set; }

        public decimal? ProductPrice { get; set; }

        public string MediaUrl { get; set; }
            = "/images/no-image.png";

        public string? ThumbnailUrl { get; set; }

        public string MediaType { get; set; } = "Image";

        public int MediaCount { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        // NEW — the actual people, not just counts.
        public List<PostLikeViewModel> Likes { get; set; } = new();

        public List<PostCommentViewModel> Comments { get; set; } = new();
    }

    // NEW
    public class PostLikeViewModel
    {
        public string CustomerName { get; set; } = "Customer";

        public DateTime CreatedAt { get; set; }
    }

    // NEW
    public class PostCommentViewModel
    {
        public string CustomerName { get; set; } = "Customer";

        public string CommentText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}