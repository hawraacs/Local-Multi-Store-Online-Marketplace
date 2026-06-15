using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
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

        public CreateModel(ApplicationDbContext context, ICurrentStoreService currentStoreService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public ProductViewModel ProductVM { get; set; } = new();

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

            ViewData["StoreName"] = store.StoreName;
            ViewData["StoreId"] = store.StoreID;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            // Trim inputs
            ProductVM.ProductName = ProductVM.ProductName?.Trim() ?? "";
            ProductVM.Description = ProductVM.Description?.Trim() ?? "";
            ProductVM.CategoryName = ProductVM.CategoryName?.Trim() ?? "";

            // Validation
            if (string.IsNullOrWhiteSpace(ProductVM.ProductName))
                ModelState.AddModelError("ProductVM.ProductName", "Product name is required.");
            if (string.IsNullOrWhiteSpace(ProductVM.CategoryName))
                ModelState.AddModelError("ProductVM.CategoryName", "Category name is required.");
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
                ViewData["StoreName"] = store.StoreName;
                return Page();
            }

            // Get or create category
            var category = await GetOrCreateCategoryAsync(ProductVM.CategoryName);

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
                OriginalPrice = ProductVM.OriginalPrice,   // ✅ save cost price
                Quantity = ProductVM.Quantity,
                LowStockThreshold = ProductVM.LowStockThreshold > 0 ? ProductVM.LowStockThreshold : 5,
                Weight = ProductVM.Weight,
                IsActive = ProductVM.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Save images
            if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                await SaveProductImages(product.ProductID, ProductVM.UploadedImages);

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
            return RedirectToPage("/StoreOwner/Products/Index");
        }

        private async Task<Category> GetOrCreateCategoryAsync(string categoryName)
        {
            var existing = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryName.ToLower() == categoryName.ToLower());
            if (existing != null)
            {
                if (!existing.IsActive) existing.IsActive = true;
                await _context.SaveChangesAsync();
                return existing;
            }

            string slug = GenerateSlug(categoryName);
            string original = slug;
            int count = 1;
            while (await _context.Categories.AnyAsync(c => c.CategorySlug == slug))
                slug = $"{original}-{count++}";

            int maxOrder = await _context.Categories.AnyAsync() ? await _context.Categories.MaxAsync(c => c.DisplayOrder) + 1 : 1;
            var newCategory = new Category
            {
                CategoryName = categoryName,
                CategorySlug = slug,
                IsActive = true,
                DisplayOrder = maxOrder
            };
            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();
            return newCategory;
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
    }
}