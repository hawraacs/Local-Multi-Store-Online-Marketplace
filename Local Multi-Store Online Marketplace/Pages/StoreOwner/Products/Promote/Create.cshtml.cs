using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;
using Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Shared;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Promote
{
    [Authorize(Roles = "StoreOwner")]
    public class CreateModel : PageModel
    {
        private const long MaxImageSize = 8 * 1024 * 1024;   // 8 MB
        private const long MaxVideoSize = 25 * 1024 * 1024;  // 25 MB

        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CreateModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public ExploreCreateInput Input { get; set; } = new();

        public List<SelectListItem> ProductOptions { get; set; } = new();

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
                    "Store was not found. Please make sure your store is approved.";

                return RedirectToPage("/StoreOwner/Dashboard");
            }

            SetStoreViewData(store);

            await LoadProductsAsync(store.StoreID);

            return Page();
        }

        [RequestSizeLimit(80 * 1024 * 1024)]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
            {
                return RedirectToPage("/Account/AccessDenied");
            }

            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store was not found.";

                return RedirectToPage("/StoreOwner/Dashboard");
            }

            SetStoreViewData(store);

            Input.Caption = Input.Caption?.Trim() ?? string.Empty;
            Input.PostType = Input.PostType?.Trim() ?? string.Empty;

            var mediaFiles = Input.UploadedMedia?
                .Where(file => file != null && file.Length > 0)
                .ToList() ?? new List<IFormFile>();

            ValidatePostType();
            await ValidateProductAsync(store.StoreID);
            await ValidateMediaAsync(mediaFiles);

            if (!ModelState.IsValid)
            {
                await LoadProductsAsync(store.StoreID);
                return Page();
            }

            string? postFolder = null;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var post = new ExplorePost
                {
                    StoreID = store.StoreID,
                    ProductID = Input.ProductID,
                    PostType = Input.PostType,
                    Caption = Input.Caption,
                    IsActive = Input.IsActive,
                    IsFeatured = false,
                    ViewCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                _context.ExplorePosts.Add(post);
                await _context.SaveChangesAsync();

                postFolder = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "uploads",
                    "explore",
                    post.ExplorePostID.ToString());

                Directory.CreateDirectory(postFolder);

                string? thumbnailUrl = null;

                if (Input.PostType == "Reel" &&
                    Input.ReelThumbnail != null &&
                    Input.ReelThumbnail.Length > 0)
                {
                    thumbnailUrl = await SaveFileAsync(
                        Input.ReelThumbnail,
                        postFolder,
                        post.ExplorePostID);
                }

                for (var index = 0; index < mediaFiles.Count; index++)
                {
                    var file = mediaFiles[index];

                    var mediaUrl = await SaveFileAsync(
                        file,
                        postFolder,
                        post.ExplorePostID);

                    _context.ExploreMedia.Add(new ExploreMedia
                    {
                        ExplorePostID = post.ExplorePostID,
                        MediaType = Input.PostType == "Reel"
                            ? "Video"
                            : "Image",
                        MediaUrl = mediaUrl,
                        ThumbnailUrl = Input.PostType == "Reel"
                            ? thumbnailUrl
                            : null,
                        DisplayOrder = index,
                        DurationSeconds = null
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    Input.PostType == "Reel"
                        ? "Reel published successfully."
                        : "Explore post published successfully.";

                return RedirectToPage("/StoreOwner/Products/Promote/Index");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                if (!string.IsNullOrWhiteSpace(postFolder) &&
                    Directory.Exists(postFolder))
                {
                    Directory.Delete(postFolder, true);
                }

                ModelState.AddModelError(
                    string.Empty,
                    "The post could not be published. Please try again.");

                await LoadProductsAsync(store.StoreID);

                return Page();
            }
        }

        private void ValidatePostType()
        {
            var allowedTypes = new[]
            {
                "Image",
                "Carousel",
                "Reel"
            };

            if (!allowedTypes.Contains(
                    Input.PostType,
                    StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "Input.PostType",
                    "Please select Image, Carousel, or Reel.");
            }
        }

        private async Task ValidateProductAsync(int storeId)
        {
            if (!Input.ProductID.HasValue)
                return;

            var productExists = await _context.Products.AnyAsync(product =>
                product.ProductID == Input.ProductID.Value &&
                product.StoreID == storeId &&
                product.IsActive);

            if (!productExists)
            {
                ModelState.AddModelError(
                    "Input.ProductID",
                    "The selected product does not belong to your store.");
            }
        }

        private async Task ValidateMediaAsync(List<IFormFile> mediaFiles)
        {
            if (!mediaFiles.Any())
            {
                ModelState.AddModelError(
                    "Input.UploadedMedia",
                    "Please upload media for the post.");

                return;
            }

            if (Input.PostType == "Image")
            {
                if (mediaFiles.Count != 1)
                {
                    ModelState.AddModelError(
                        "Input.UploadedMedia",
                        "An image post must contain exactly one image.");
                }

                foreach (var file in mediaFiles)
                {
                    await ValidateImageAsync(file, "Input.UploadedMedia");
                }
            }

            if (Input.PostType == "Carousel")
            {
                if (mediaFiles.Count < 2 || mediaFiles.Count > 10)
                {
                    ModelState.AddModelError(
                        "Input.UploadedMedia",
                        "A carousel must contain between 2 and 10 images.");
                }

                foreach (var file in mediaFiles)
                {
                    await ValidateImageAsync(file, "Input.UploadedMedia");
                }
            }

            if (Input.PostType == "Reel")
            {
                if (mediaFiles.Count != 1)
                {
                    ModelState.AddModelError(
                        "Input.UploadedMedia",
                        "A reel must contain exactly one video.");
                }
                else
                {
                    ValidateVideo(mediaFiles[0], "Input.UploadedMedia");
                }

                if (Input.ReelThumbnail != null &&
                    Input.ReelThumbnail.Length > 0)
                {
                    await ValidateImageAsync(
                        Input.ReelThumbnail,
                        "Input.ReelThumbnail");
                }
            }
        }

        /// <summary>
        /// Extension/MIME/size checks are delegated to the shared
        /// ProductMediaValidator (see Products/Shared/ProductMediaValidator.cs).
        /// BUGFIX (carried over from the merge): the original Explore validation
        /// never checked the file's actual magic bytes, only its extension —
        /// Products/Create and Products/Edit always did. Post media now gets the
        /// same signature check, so a renamed non-image can't slip past here
        /// either.
        /// </summary>
        private async Task ValidateImageAsync(IFormFile file, string key)
        {
            var basicError = ProductMediaValidator.ValidateImageBasics(file, MaxImageSize);
            if (basicError != null)
            {
                ModelState.AddModelError(key, basicError);
                return;
            }

            if (!await ProductMediaValidator.HasValidImageSignatureAsync(file))
            {
                ModelState.AddModelError(key, $"{file.FileName}: doesn't look like a valid image file.");
            }
        }

        private void ValidateVideo(IFormFile file, string key)
        {
            var basicError = ProductMediaValidator.ValidateVideoBasics(file, MaxVideoSize);
            if (basicError != null)
            {
                ModelState.AddModelError(key, basicError);
            }
        }

        private async Task<string> SaveFileAsync(
            IFormFile file,
            string folder,
            int postId)
        {
            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folder, fileName);

            await using var stream = new FileStream(
                filePath,
                FileMode.CreateNew);

            await file.CopyToAsync(stream);

            return $"/uploads/explore/{postId}/{fileName}";
        }

        private async Task LoadProductsAsync(int storeId)
        {
            ProductOptions = await _context.Products
                .AsNoTracking()
                .Where(product =>
                    product.StoreID == storeId &&
                    product.IsActive)
                .OrderBy(product => product.ProductName)
                .Select(product => new SelectListItem
                {
                    Value = product.ProductID.ToString(),
                    Text = $"{product.ProductName} - ${product.Price:N2}"
                })
                .ToListAsync();
        }

        private void SetStoreViewData(Store store)
        {
            ViewData["StoreName"] = store.StoreName;
            ViewData["StoreId"] = store.StoreID;
        }
    }

    public class ExploreCreateInput
    {
        [Required(ErrorMessage = "Please select a post type.")]
        public string PostType { get; set; } = "Image";

        [MaxLength(
            2200,
            ErrorMessage = "Caption cannot exceed 2200 characters.")]
        public string Caption { get; set; } = string.Empty;

        [Display(Name = "Linked Product")]
        public int? ProductID { get; set; }

        [Display(Name = "Post Media")]
        public List<IFormFile> UploadedMedia { get; set; } = new();

        [Display(Name = "Reel Thumbnail")]
        public IFormFile? ReelThumbnail { get; set; }

        public bool IsActive { get; set; } = true;
    }
}