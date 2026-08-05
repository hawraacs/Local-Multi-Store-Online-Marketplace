using java.awt;
using Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Core.ViewModels.StoreOwner;
using Multi_Store.Infrastructure.Data;
using SkiaSharp;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ILogger<CreateModel> _logger;

        private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB per image
        private const int MaxImageCount = 5;

        // Every uploaded product image is center-cropped to a square and
        // resized to exactly this size, so it always fills a square tile
        // cleanly (Explore grid/modal, Feed) instead of relying on
        // whatever raw dimensions the store owner happened to upload.
        private const int ProductImageTargetSize = 1080;

        public CreateModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration,
            IAuditLogRepository auditLogRepository,
            ILogger<CreateModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _auditLogRepository = auditLogRepository;
            _logger = logger;
        }

        [BindProperty]
        public ProductViewModel ProductVM { get; set; } = new();

        // Nested category tree (roots only; each node's own Children is populated).
        // Replaces the old flat "CategoriesSelectList" — the picker in the view
        // renders parents with their children directly nested beneath them.
        public List<CategoryTreeItem> CategoryTree { get; set; } = new();

        // =============================================================
        // ON GET – Check subscription status
        // =============================================================
        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                    return RedirectToPage("/Account/AccessDenied");

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found. Please ensure your store is approved.";
                    return RedirectToPage("/StoreOwner/Dashboard");
                }

                // ✅ Check if subscription is active (trial or paid)
                if (!IsSubscriptionActive(store))
                {
                    // Create or get a pending payment record for monthly subscription
                    var pendingPayment = await GetOrCreatePendingSubscriptionPaymentAsync(store.StoreID);

                    if (pendingPayment == null)
                    {
                        TempData["ErrorMessage"] = "Unable to create payment request. Please try again.";
                        return RedirectToPage("/StoreOwner/Products/Index");
                    }

                    // Redirect to the payment page with the payment ID
                    // After successful payment, return to this page.
                    return RedirectToPage("/StoreOwner/StoreOwnerPayment", new
                    {
                        paymentId = pendingPayment.StorePaymentId,
                        returnUrl = Url.Page("/StoreOwner/Products/Create")
                    });
                }

                // ✅ Subscription active – allowed to create product
                ViewData["StoreName"] = store.StoreName;
                ViewData["StoreId"] = store.StoreID;

                await LoadCategories();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Create Product page for user {UserId}.", User?.Identity?.Name);
                TempData["ErrorMessage"] = "Something went wrong while loading this page. Please try again.";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
        }

        // =============================================================
        // ON POST – Product creation logic
        // =============================================================
        public async Task<IActionResult> OnPostAsync()
        {
            // Extra safety: re-check subscription on POST to prevent bypass
            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            if (!IsSubscriptionActive(store))
            {
                TempData["ErrorMessage"] = "Your subscription has expired. Please renew to add products.";
                return RedirectToPage("/StoreOwner/Products/Index");
            }

            ProductVM.ProductName = ProductVM.ProductName?.Trim() ?? "";
            ProductVM.Description = ProductVM.Description?.Trim() ?? "";

            // Validation
            if (string.IsNullOrWhiteSpace(ProductVM.ProductName))
                ModelState.AddModelError("ProductVM.ProductName", "Product name is required.");
            if (ProductVM.CategoryID <= 0)
                ModelState.AddModelError("ProductVM.CategoryID", "Please select a category.");
            if (ProductVM.Price <= 0)
                ModelState.AddModelError("ProductVM.Price", "Price must be greater than 0.");
            if (ProductVM.Quantity < 0)
                ModelState.AddModelError("ProductVM.Quantity", "Quantity cannot be negative.");

            // BUGFIX: the form marks "Your Cost Price" as required (red asterisk) but
            // nothing ever enforced that server-side — OriginalPrice has no [Required]
            // attribute, and the two checks below only fire when a value IS present.
            // Leaving it blank silently created products with OriginalPrice == null,
            // permanently breaking profit/margin tracking for that product.
            if (!ProductVM.OriginalPrice.HasValue)
                ModelState.AddModelError("ProductVM.OriginalPrice", "Cost price is required.");
            if (ProductVM.OriginalPrice.HasValue && ProductVM.OriginalPrice.Value < 0)
                ModelState.AddModelError("ProductVM.OriginalPrice", "Cost price cannot be negative.");
            if (ProductVM.OriginalPrice.HasValue && ProductVM.Price < ProductVM.OriginalPrice.Value)
                ModelState.AddModelError("ProductVM.OriginalPrice", "Selling price should be higher than cost price.");

            // BUGFIX: previously there was no validation at all on uploaded images —
            // any file type, any size, any count could be posted directly (the "max 5
            // images" text was decorative only). Validated here, before the product is
            // ever created, so a bad upload doesn't leave an orphaned product behind.
            var imageError = await ValidateUploadedImagesAsync(ProductVM.UploadedImages);
            if (imageError != null)
            {
                ModelState.AddModelError("ProductVM.UploadedImages", imageError);
            }

            if (!ModelState.IsValid)
            {
                await LoadCategories();
                ViewData["StoreName"] = store.StoreName;
                return Page();
            }

            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryID == ProductVM.CategoryID && c.IsActive);

                if (category == null)
                {
                    ModelState.AddModelError("", "Selected category is invalid.");
                    await LoadCategories();
                    return Page();
                }

                // Generate unique slug
                string slug = GenerateSlug(ProductVM.ProductName);
                string originalSlug = slug;
                int counter = 1;
                while (await _context.Products.AnyAsync(p => p.ProductSlug == slug && p.StoreID == store.StoreID))
                    slug = $"{originalSlug}-{counter++}";

                var product = new Product
                {
                    StoreID = store.StoreID,
                    CategoryID = category.CategoryID,
                    ProductName = ProductVM.ProductName,
                    ProductSlug = slug,
                    Description = ProductVM.Description,
                    Price = ProductVM.Price,
                    CompareAtPrice = ProductVM.CompareAtPrice,
                    OriginalPrice = ProductVM.OriginalPrice,
                    Quantity = ProductVM.Quantity,
                    LowStockThreshold = ProductVM.LowStockThreshold > 0 ? ProductVM.LowStockThreshold : 5,
                    Weight = ProductVM.Weight,
                    IsActive = ProductVM.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = "Unknown";
                }

                await _auditLogRepository.AddAsync(new AuditLog
                {
                    UserID = store.OwnerUserID,
                    Action = "CreateProduct",
                    EntityName = "Product",
                    EntityID = product.ProductID.ToString(),
                    OldValue = null,
                    NewValue = $"Product created: {product.ProductName}",
                    IPAddress = ipAddress,
                    UserAgent = userAgent,
                    ActionDate = DateTime.UtcNow
                });

                // Save images — already validated above, but the actual file I/O can
                // still fail (disk, permissions, etc.), so it's wrapped separately:
                // the product itself is already safely created at this point, so a
                // storage failure shouldn't surface as an unhandled exception page.
                if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                {
                    try
                    {
                        await SaveProductImages(product.ProductID, ProductVM.UploadedImages);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Product {ProductId} was created but its images failed to save.", product.ProductID);
                        TempData["SuccessMessage"] = $"Product '{product.ProductName}' was created, but its images couldn't be uploaded. You can add them from the Edit page.";
                        return RedirectToPage("/StoreOwner/Products/Index");
                    }
                }

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product for Store {StoreId}.", store.StoreID);
                ModelState.AddModelError("", "Something went wrong while creating the product. Please try again.");
                await LoadCategories();
                return Page();
            }
        }

        // =============================================================
        // HELPER METHODS
        // =============================================================

        /// <summary>
        /// Builds the real category tree (parents with their children nested
        /// directly beneath them, to any depth) for the category picker.
        /// </summary>
        private async Task LoadCategories()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var byParent = categories.ToLookup(c => c.ParentCategoryID);

            List<CategoryTreeItem> BuildChildren(int? parentId, int depth, string parentPath)
            {
                var nodes = new List<CategoryTreeItem>();

                foreach (var cat in byParent[parentId].OrderBy(c => c.CategoryName))
                {
                    var path = string.IsNullOrEmpty(parentPath)
                        ? cat.CategoryName
                        : $"{parentPath} › {cat.CategoryName}";

                    var node = new CategoryTreeItem
                    {
                        CategoryId = cat.CategoryID,
                        Name = cat.CategoryName,
                        Depth = depth,
                        Path = path
                    };
                    node.Children = BuildChildren(cat.CategoryID, depth + 1, path);
                    nodes.Add(node);
                }

                return nodes;
            }

            CategoryTree = BuildChildren(null, 0, "");
        }

        /// <summary>
        /// Finds the "Grandparent › Parent › Name" breadcrumb for a given category id,
        /// used to pre-fill the picker's label when redisplaying the form (e.g. after
        /// a validation error) so the admin still sees what they had selected.
        /// </summary>
        public static string? FindCategoryPath(List<CategoryTreeItem> nodes, int id)
        {
            foreach (var node in nodes)
            {
                if (node.CategoryId == id) return node.Path;

                var found = FindCategoryPath(node.Children, id);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Validates uploaded product images: count, then per-file rules delegated
        /// to <see cref="ProductMediaValidator"/> (the shared logic that used to be
        /// duplicated here and in Edit.cshtml.cs).
        /// </summary>
        private static async Task<string?> ValidateUploadedImagesAsync(List<IFormFile>? images)
        {
            if (images == null || images.Count == 0)
                return null; // images are optional

            if (images.Count > MaxImageCount)
                return $"You can upload at most {MaxImageCount} images.";

            foreach (var image in images)
            {
                if (image.Length == 0) continue;

                var basicError = ProductMediaValidator.ValidateImageBasics(image, MaxImageSizeBytes);
                if (basicError != null) return basicError;

                if (!await ProductMediaValidator.HasValidImageSignatureAsync(image))
                    return $"'{image.FileName}' doesn't look like a valid image file.";
            }

            return null;
        }

        /// <summary>
        /// Saves uploaded product images after center-cropping each one to a
        /// square and resizing to a fixed size (ProductImageTargetSize).
        ///
        /// CHANGED — this previously just copied the raw uploaded file
        /// straight to disk with no processing at all. Whatever aspect ratio
        /// a store owner happened to upload (portrait, landscape, odd crops)
        /// went straight into the grid/feed/modal, which all assume a square
        /// tile — that mismatch is what caused the black letterboxing in the
        /// Explore modal (object-fit: contain padding out the gaps with its
        /// background) and blurry/stretched images in the Feed. Every image
        /// is now normalized at upload time instead, the same way Instagram
        /// crops to a fixed square before it's ever stored.
        ///
        /// Uses SkiaSharp rather than SixLabors.ImageSharp — ImageSharp's
        /// newer versions started requiring a commercial license key at
        /// runtime for some usage tiers, which isn't something to build a
        /// dependency on here. SkiaSharp is MIT-licensed with no license
        /// key of any kind required.
        /// </summary>
        private async Task SaveProductImages(int productId, List<IFormFile> images)
        {
            string folder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", productId.ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img.Length > 0)
                {
                    string fileName = $"{Guid.NewGuid()}.jpg";
                    string filePath = Path.Combine(folder, fileName);

                    using (var inputStream = img.OpenReadStream())
                    using (var original = SKBitmap.Decode(inputStream))
                    {
                        if (original == null)
                        {
                            throw new InvalidOperationException($"Could not decode image '{img.FileName}'.");
                        }

                        var shortestSide = Math.Min(original.Width, original.Height);
                        var cropX = (original.Width - shortestSide) / 2;
                        var cropY = (original.Height - shortestSide) / 2;

                        // Crop and resize in one draw call — rather than crop
                        // then call a separate .Resize(), which depends on
                        // SKFilterQuality (deprecated/removed across recent
                        // SkiaSharp versions in favor of SKSamplingOptions).
                        // Drawing straight from the cropped source rect into a
                        // destination bitmap already at the target size does
                        // both steps with one long-stable API.
                        var sourceRect = new SKRectI(cropX, cropY, cropX + shortestSide, cropY + shortestSide);
                        var destRect = new SKRect(0, 0, ProductImageTargetSize, ProductImageTargetSize);

                        using var squared = new SKBitmap(ProductImageTargetSize, ProductImageTargetSize);
                        using (var canvas = new SKCanvas(squared))
                        {
                            canvas.Clear(SKColors.White);
                            canvas.DrawBitmap(original, sourceRect, destRect);
                        }

                        using var skImage = SKImage.FromBitmap(squared);
                        using var encodedData = skImage.Encode(SKEncodedImageFormat.Jpeg, 88);

                        using var fileStream = new FileStream(filePath, FileMode.Create);
                        encodedData.SaveTo(fileStream);
                    }

                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductID = productId,
                        ImageUrl = $"/uploads/products/{productId}/{fileName}",
                        DisplayOrder = i,
                        IsPrimary = i == 0
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        private string GenerateSlug(string name)
        {
            string slug = name.ToLower().Trim().Replace(" ", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }

        // =============================================================
        // SUBSCRIPTION HELPERS
        // =============================================================

        /// <summary>
        /// Checks if the store has an active subscription (trial or paid).
        /// </summary>
        private bool IsSubscriptionActive(Store store)
        {
            // Trial period (first month free)
            if (store.SubscriptionStatus?.Equals("Trial", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate.Value > DateTime.UtcNow)
                    return true;
            }

            // Paid subscription
            if (store.SubscriptionStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate.Value > DateTime.UtcNow)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets or creates a pending payment for the monthly subscription fee.
        /// </summary>
        private async Task<StorePayment?> GetOrCreatePendingSubscriptionPaymentAsync(int storeId)
        {
            decimal monthlyFee = _configuration.GetValue<decimal>("StoreSettings:MonthlySubscriptionFee", 20.00m);
            const string description = "Monthly Subscription Fee";

            // Check for existing pending payment
            var existing = await _context.StorePayments
                .FirstOrDefaultAsync(sp => sp.StoreId == storeId
                                           && sp.Description == description
                                           && sp.Status == "Pending");

            if (existing != null)
                return existing;

            // Create new pending payment
            var payment = new StorePayment
            {
                StoreId = storeId,
                Amount = monthlyFee,
                Description = description,
                DueDate = DateTime.UtcNow.AddDays(7),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.StorePayments.Add(payment);
            await _context.SaveChangesAsync();

            return payment;
        }
    }

    public class CategoryTreeItem
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Depth { get; set; }
        public string Path { get; set; } = string.Empty; // e.g. "Electronics › Phones"
        public List<CategoryTreeItem> Children { get; set; } = new();
    }
}