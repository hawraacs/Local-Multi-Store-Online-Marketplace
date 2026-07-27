using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.ViewModels.StoreOwner;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<EditModel> _logger;

        // NOTE: duplicated from CreateModel.cs — both pages need identical image
        // validation and there's currently no shared service to put this in.
        // Worth extracting into something like an IProductImageValidator if a
        // third place ever needs the same rules.
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB per image
        private const int MaxImageCount = 5; // total, existing + new combined

        public EditModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<EditModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [BindProperty]
        public ProductViewModel ProductVM { get; set; } = new();

        [BindProperty]
        public List<int> ImagesToDelete { get; set; } = new();

        public List<SelectListItem> CategoriesSelectList { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                    return RedirectToPage("/Account/AccessDenied");

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found.";
                    return RedirectToPage("/StoreOwner/Dashboard");
                }

                var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductID == id && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                await LoadCategoriesSelectList();

                ProductVM = new ProductViewModel
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Description = product.Description,
                    Price = product.Price,
                    CompareAtPrice = product.CompareAtPrice,
                    OriginalPrice = product.OriginalPrice,
                    Quantity = product.Quantity,
                    LowStockThreshold = product.LowStockThreshold,
                    Weight = product.Weight,
                    CategoryID = product.CategoryID,
                    IsActive = product.IsActive,
                    ExistingImages = product.Images.Select(img => new ProductImageViewModel
                    {
                        ImageID = img.ImageID,
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder,
                        IsPrimary = img.IsPrimary
                    }).OrderBy(i => i.DisplayOrder).ToList()
                };

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Edit Product page for product {ProductId}.", id);
                TempData["ErrorMessage"] = "Something went wrong while loading this product. Please try again.";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            try
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == ProductVM.ProductID && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                ProductVM.ProductName = ProductVM.ProductName?.Trim() ?? "";
                ProductVM.Description = ProductVM.Description?.Trim() ?? "";

                // Manual validation — mirrors CreateModel.cs so both pages behave
                // consistently. None of this existed before beyond ModelState.IsValid,
                // which relies purely on data annotations.
                if (string.IsNullOrWhiteSpace(ProductVM.ProductName))
                    ModelState.AddModelError("ProductVM.ProductName", "Product name is required.");
                if (ProductVM.CategoryID <= 0)
                    ModelState.AddModelError("ProductVM.CategoryID", "Please select a category.");
                if (ProductVM.Price <= 0)
                    ModelState.AddModelError("ProductVM.Price", "Price must be greater than 0.");
                if (ProductVM.Quantity < 0)
                    ModelState.AddModelError("ProductVM.Quantity", "Quantity cannot be negative.");

                // BUGFIX: "Your Cost Price" is marked required in the UI, but nothing
                // enforced that server-side. On Edit this was arguably worse than on
                // Create — a store owner could clear cost price on an already-priced
                // product and silently wipe its profit/margin tracking going forward.
                if (!ProductVM.OriginalPrice.HasValue)
                    ModelState.AddModelError("ProductVM.OriginalPrice", "Cost price is required.");
                if (ProductVM.OriginalPrice.HasValue && ProductVM.OriginalPrice.Value < 0)
                    ModelState.AddModelError("ProductVM.OriginalPrice", "Cost price cannot be negative.");
                if (ProductVM.OriginalPrice.HasValue && ProductVM.Price < ProductVM.OriginalPrice.Value)
                    ModelState.AddModelError("ProductVM.OriginalPrice", "Selling price should be higher than cost price.");

                // BUGFIX: previously zero validation on new images — no type, size,
                // or count checks at all.
                var imageError = await ValidateUploadedImagesAsync(ProductVM.UploadedImages);
                if (imageError != null)
                {
                    ModelState.AddModelError("ProductVM.UploadedImages", imageError);
                }

                // BUGFIX: no cap existed on total images per product — existing minus
                // what's being deleted, plus new uploads, could grow without bound
                // across repeated edits.
                var deleteSet = new HashSet<int>(ImagesToDelete ?? new List<int>());
                int remainingExistingCount = product.Images.Count(img => !deleteSet.Contains(img.ImageID));
                int newImageCount = ProductVM.UploadedImages?.Count(f => f.Length > 0) ?? 0;
                if (remainingExistingCount + newImageCount > MaxImageCount)
                {
                    ModelState.AddModelError(
                        "ProductVM.UploadedImages",
                        $"A product can have at most {MaxImageCount} images total. " +
                        $"You'd have {remainingExistingCount} existing plus {newImageCount} new.");
                }

                if (!ModelState.IsValid)
                {
                    await LoadCategoriesSelectList();

                    // BUGFIX: ExistingImages is only populated in OnGetAsync, never from
                    // the POST body — redisplaying the form after a validation error used
                    // to show "No images uploaded yet" even though the product's images
                    // were untouched in the database. Re-fetched here so the form
                    // accurately reflects what's actually still there.
                    ProductVM.ExistingImages = product.Images
                        .Select(img => new ProductImageViewModel
                        {
                            ImageID = img.ImageID,
                            ImageUrl = img.ImageUrl,
                            DisplayOrder = img.DisplayOrder,
                            IsPrimary = img.IsPrimary
                        })
                        .OrderBy(i => i.DisplayOrder)
                        .ToList();

                    return Page();
                }

                // Update slug if name changed
                if (product.ProductName != ProductVM.ProductName)
                {
                    string newSlug = GenerateSlug(ProductVM.ProductName);
                    string originalSlug = newSlug;
                    int counter = 1;
                    while (await _context.Products.AnyAsync(p => p.ProductSlug == newSlug && p.StoreID == store.StoreID && p.ProductID != product.ProductID))
                        newSlug = $"{originalSlug}-{counter++}";
                    product.ProductSlug = newSlug;
                }

                product.ProductName = ProductVM.ProductName;
                product.Description = ProductVM.Description;
                product.Price = ProductVM.Price;
                product.CompareAtPrice = ProductVM.CompareAtPrice;
                product.OriginalPrice = ProductVM.OriginalPrice;
                product.Quantity = ProductVM.Quantity;
                product.LowStockThreshold = ProductVM.LowStockThreshold;
                product.Weight = ProductVM.Weight;
                product.CategoryID = ProductVM.CategoryID;
                product.IsActive = ProductVM.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Delete selected images
                if (ImagesToDelete.Any())
                    await DeleteImages(product.ProductID, ImagesToDelete);

                // Upload new images
                if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                    await AddNewImages(product.ProductID, ProductVM.UploadedImages);

                // Ensure at least one primary image
                await EnsurePrimaryImage(product.ProductID);

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' updated successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Product {ProductId} for Store.", ProductVM?.ProductID);
                ModelState.AddModelError("", "Something went wrong while saving your changes. Please try again.");
                await LoadCategoriesSelectList();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSetPrimaryImageAsync(int imageId, int productId)
        {
            try
            {
                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null) return new JsonResult(new { success = false, message = "Store not found" });

                var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.ProductID == productId && p.StoreID == store.StoreID);
                if (product == null) return new JsonResult(new { success = false, message = "Product not found" });

                foreach (var img in product.Images) img.IsPrimary = false;
                var primary = product.Images.FirstOrDefault(i => i.ImageID == imageId);
                if (primary != null) primary.IsPrimary = true;
                await _context.SaveChangesAsync();
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary image {ImageId} for product {ProductId}.", imageId, productId);
                return new JsonResult(new { success = false, message = "Something went wrong. Please try again." });
            }
        }

        private async Task LoadCategoriesSelectList()
        {
            var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
            CategoriesSelectList = categories.Select(c => new SelectListItem(c.CategoryName, c.CategoryID.ToString())).ToList();
        }

        private async Task DeleteImages(int productId, List<int> imageIds)
        {
            // BUGFIX (security): this previously had NO ownership check at all —
            //   var images = await _context.ProductImages.Where(i => imageIds.Contains(i.ImageID)).ToListAsync();
            // The "ImagesToDelete" hidden inputs are ordinary form fields; a store
            // owner could edit the POST body (or script a raw request) to include
            // an image ID belonging to a completely different store's product, and
            // this code would delete that file from disk and its DB row with zero
            // ownership check. Now scoped to the specific product being edited,
            // which the caller has already verified belongs to the current store.
            var images = await _context.ProductImages
                .Where(i => imageIds.Contains(i.ImageID) && i.ProductID == productId)
                .ToListAsync();

            foreach (var img in images)
            {
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, img.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
                _context.ProductImages.Remove(img);
            }
            await _context.SaveChangesAsync();
        }

        private async Task AddNewImages(int productId, List<IFormFile> images)
        {
            string folder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", productId.ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            int maxOrder = await _context.ProductImages.Where(i => i.ProductID == productId).MaxAsync(i => (int?)i.DisplayOrder) ?? -1;
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img.Length > 0)
                {
                    string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(img.FileName)}";
                    string filePath = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await img.CopyToAsync(stream);

                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductID = productId,
                        ImageUrl = $"/uploads/products/{productId}/{fileName}",
                        DisplayOrder = maxOrder + i + 1,
                        IsPrimary = false
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task EnsurePrimaryImage(int productId)
        {
            var images = await _context.ProductImages.Where(i => i.ProductID == productId).ToListAsync();
            if (images.Any() && !images.Any(i => i.IsPrimary))
            {
                var first = images.OrderBy(i => i.DisplayOrder).First();
                first.IsPrimary = true;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Validates newly uploaded product images: count, size, extension/MIME type,
        /// and the actual file signature (magic bytes) — extension and Content-Type
        /// are both client-supplied and easily spoofed, so the header bytes are
        /// checked too before anything is trusted or saved to disk.
        /// </summary>
        private static async Task<string?> ValidateUploadedImagesAsync(List<IFormFile>? images)
        {
            if (images == null || images.Count == 0)
                return null; // new images are optional on edit

            foreach (var image in images)
            {
                if (image.Length == 0) continue;

                if (image.Length > MaxImageSizeBytes)
                    return $"'{image.FileName}' is too large. Maximum size per image is 5 MB.";

                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                var contentType = image.ContentType?.ToLowerInvariant();

                if (!AllowedImageExtensions.Contains(extension) || !AllowedImageMimeTypes.Contains(contentType))
                    return $"'{image.FileName}' isn't a supported image type. Use JPG, PNG, or WEBP.";

                if (!await HasValidImageSignatureAsync(image))
                    return $"'{image.FileName}' doesn't look like a valid image file.";
            }

            return null;
        }

        private static async Task<bool> HasValidImageSignatureAsync(IFormFile file)
        {
            var buffer = new byte[12];
            int bytesRead;

            using (var stream = file.OpenReadStream())
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            }

            if (bytesRead < 4) return false;

            bool StartsWith(byte[] signature, int offset = 0)
            {
                if (buffer.Length < offset + signature.Length) return false;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (buffer[offset + i] != signature[i]) return false;
                }
                return true;
            }

            if (StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return true;                 // JPEG
            if (StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return true;            // PNG
            if (bytesRead >= 12 &&
                StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0) &&                    // "RIFF"
                StartsWith(new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8))                       // "WEBP"
                return true;

            return false;
        }

        private string GenerateSlug(string name)
        {
            string slug = name.ToLower().Trim().Replace(" ", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }
    }
}
