using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Promote
{
    [Authorize(Roles = "StoreOwner")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;

        public EditModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService)
        {
            _context = context;
            _currentStoreService = currentStoreService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public int ExplorePostID { get; set; }

        // Media itself isn't editable here (same convention most platforms
        // use — swapping the actual photo/video on an existing post is
        // unusual; the store owner can delete and re-create instead).
        // It's shown read-only so the owner can see what they're editing.
        public string PostType { get; set; } = "Image";

        public string StoreName { get; set; } = string.Empty;

        public List<MediaPreviewViewModel> Media { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

        public class InputModel
        {
            [StringLength(2200, ErrorMessage = "Caption can be at most 2200 characters.")]
            public string? Caption { get; set; }

            public int? ProductID { get; set; }

            public bool IsFeatured { get; set; }

            public bool IsActive { get; set; }
        }

        public class MediaPreviewViewModel
        {
            public string MediaType { get; set; } = "Image";

            public string MediaUrl { get; set; } = string.Empty;

            public string? ThumbnailUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
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

            // AsNoTracking would be fine for a GET, but we intentionally
            // don't use it here since the exact same query shape is reused
            // (tracked) on the POST below where we actually mutate the post.
            var post = await _context.ExplorePosts
                .Include(p => p.Media)
                .FirstOrDefaultAsync(p =>
                    p.ExplorePostID == id &&
                    p.StoreID == store.StoreID);

            if (post == null)
            {
                TempData["ErrorMessage"] = "Explore post was not found.";
                return RedirectToPage("/StoreOwner/Products/Promote/Index");
            }

            ExplorePostID = post.ExplorePostID;
            PostType = post.PostType;

            Input = new InputModel
            {
                Caption = post.Caption,
                ProductID = post.ProductID,
                IsFeatured = post.IsFeatured,
                IsActive = post.IsActive
            };

            Media = BuildMediaPreview(post.Media);

            await LoadProductOptionsAsync(store.StoreID);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store was not found.";
                return RedirectToPage("/StoreOwner/Products/Promote/Index");
            }

            StoreName = store.StoreName;
            ViewData["StoreName"] = store.StoreName;
            ViewData["StoreId"] = store.StoreID;

            var post = await _context.ExplorePosts
                .Include(p => p.Media)
                .FirstOrDefaultAsync(p =>
                    p.ExplorePostID == id &&
                    p.StoreID == store.StoreID);

            if (post == null)
            {
                TempData["ErrorMessage"] = "Explore post was not found.";
                return RedirectToPage("/StoreOwner/Products/Promote/Index");
            }

            ExplorePostID = post.ExplorePostID;
            PostType = post.PostType;

            // Guard against tampering with the <select> to point at a
            // product that isn't even this owner's — a plain client-side
            // dropdown value is trivial to edit in devtools.
            if (Input.ProductID.HasValue)
            {
                var belongsToStore = await _context.Products.AnyAsync(p =>
                    p.ProductID == Input.ProductID.Value &&
                    p.StoreID == store.StoreID);

                if (!belongsToStore)
                {
                    ModelState.AddModelError(
                        "Input.ProductID",
                        "Please choose a valid product from your own store.");
                }
            }

            if (!ModelState.IsValid)
            {
                Media = BuildMediaPreview(post.Media);
                await LoadProductOptionsAsync(store.StoreID);
                return Page();
            }

            post.Caption = Input.Caption?.Trim() ?? string.Empty;
            post.ProductID = Input.ProductID;
            post.IsFeatured = Input.IsFeatured;
            post.IsActive = Input.IsActive;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Post updated successfully.";

            return RedirectToPage("/StoreOwner/Products/Promote/Index");
        }

        private static List<MediaPreviewViewModel> BuildMediaPreview(
            IEnumerable<Multi_Store.Core.Entities.ExploreMedia> media)
        {
            return media
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new MediaPreviewViewModel
                {
                    MediaType = m.MediaType,
                    MediaUrl = m.MediaUrl,
                    ThumbnailUrl = m.ThumbnailUrl
                })
                .ToList();
        }

        private async Task LoadProductOptionsAsync(int storeId)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreID == storeId && p.IsActive)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ProductOptions = products
                .Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = $"{p.ProductName} — ${p.Price:N2}"
                })
                .ToList();
        }
    }
}