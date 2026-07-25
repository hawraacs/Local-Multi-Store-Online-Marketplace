using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Core.ViewModels.StoreOwner;
using Multi_Store.Infrastructure.Data;

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

        public CreateModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration,
            IAuditLogRepository auditLogRepository)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _auditLogRepository = auditLogRepository;
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
            if (ProductVM.OriginalPrice.HasValue && ProductVM.OriginalPrice.Value < 0)
                ModelState.AddModelError("ProductVM.OriginalPrice", "Cost price cannot be negative.");
            if (ProductVM.OriginalPrice.HasValue && ProductVM.Price < ProductVM.OriginalPrice.Value)
                ModelState.AddModelError("ProductVM.OriginalPrice", "Selling price should be higher than cost price.");

            if (!ModelState.IsValid)
            {
                await LoadCategories();
                ViewData["StoreName"] = store.StoreName;
                return Page();
            }

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

            // Save images
            if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                await SaveProductImages(product.ProductID, ProductVM.UploadedImages);

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
            return RedirectToPage("/StoreOwner/Products/Index");
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

        private async Task SaveProductImages(int productId, List<IFormFile> images)
        {
            string folder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", productId.ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

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
