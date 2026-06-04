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
        public ProductViewModel ProductVM { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
            {
                return RedirectToPage("/Account/AccessDenied");
            }

            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found. Please ensure your store is approved.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            ViewData["StoreName"] = store.StoreName;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    return RedirectToPage("/Account/AccessDenied");
                }

                var store = await _currentStoreService.GetCurrentStoreAsync();

                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found. Please contact support.";
                    return RedirectToPage("/StoreOwner/Dashboard");
                }

                ProductVM.ProductName = ProductVM.ProductName?.Trim() ?? string.Empty;
                ProductVM.Description = ProductVM.Description?.Trim() ?? string.Empty;
                ProductVM.CategoryName = ProductVM.CategoryName?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(ProductVM.ProductName))
                {
                    ModelState.AddModelError("ProductVM.ProductName", "Product name is required.");
                }

                if (string.IsNullOrWhiteSpace(ProductVM.CategoryName))
                {
                    ModelState.AddModelError("ProductVM.CategoryName", "Category name is required.");
                }

                if (ProductVM.Price <= 0)
                {
                    ModelState.AddModelError("ProductVM.Price", "Price must be greater than 0.");
                }

                if (ProductVM.Quantity < 0)
                {
                    ModelState.AddModelError("ProductVM.Quantity", "Quantity cannot be negative.");
                }

                if (!ModelState.IsValid)
                {
                    ViewData["StoreName"] = store.StoreName;
                    return Page();
                }

                var category = await GetOrCreateCategoryAsync(ProductVM.CategoryName);

                string productSlug = GenerateSlug(ProductVM.ProductName);
                int counter = 1;
                string originalSlug = productSlug;

                while (await _context.Products.AnyAsync(p =>
                    p.ProductSlug == productSlug &&
                    p.StoreID == store.StoreID))
                {
                    productSlug = $"{originalSlug}-{counter++}";
                }

                var product = new Product
                {
                    StoreID = store.StoreID,
                    CategoryID = category.CategoryID,
                    ProductName = ProductVM.ProductName,
                    ProductSlug = productSlug,
                    Description = ProductVM.Description,
                    Price = ProductVM.Price,
                    CompareAtPrice = ProductVM.CompareAtPrice,
                    Quantity = ProductVM.Quantity,
                    LowStockThreshold = ProductVM.LowStockThreshold > 0 ? ProductVM.LowStockThreshold : 5,
                    Weight = ProductVM.Weight,
                    IsActive = ProductVM.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                {
                    await SaveProductImages(product.ProductID, ProductVM.UploadedImages);
                }

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error saving product: {ex.Message}";
                return Page();
            }
        }

        private async Task<Category> GetOrCreateCategoryAsync(string categoryName)
        {
            categoryName = categoryName.Trim();

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.CategoryName.ToLower() == categoryName.ToLower());

            if (existingCategory != null)
            {
                if (!existingCategory.IsActive)
                {
                    existingCategory.IsActive = true;
                    await _context.SaveChangesAsync();
                }

                return existingCategory;
            }

            string slug = GenerateSlug(categoryName);
            string originalSlug = slug;
            int counter = 1;

            while (await _context.Categories.AnyAsync(c => c.CategorySlug == slug))
            {
                slug = $"{originalSlug}-{counter++}";
            }

            int displayOrder = 1;

            if (await _context.Categories.AnyAsync())
            {
                displayOrder = await _context.Categories.MaxAsync(c => c.DisplayOrder) + 1;
            }

            var category = new Category
            {
                CategoryName = categoryName,
                CategorySlug = slug,
                IsActive = true,
                DisplayOrder = displayOrder
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return category;
        }

        private async Task SaveProductImages(int productId, List<IFormFile> images)
        {
            string uploadFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "products",
                productId.ToString());

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];

                if (image.Length > 0)
                {
                    string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
                    string filePath = Path.Combine(uploadFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    var productImage = new ProductImage
                    {
                        ProductID = productId,
                        ImageUrl = $"/uploads/products/{productId}/{uniqueFileName}",
                        DisplayOrder = i,
                        IsPrimary = i == 0
                    };

                    _context.ProductImages.Add(productImage);
                }
            }

            await _context.SaveChangesAsync();
        }

        private string GenerateSlug(string name)
        {
            string slug = name.ToLower().Trim();
            slug = slug.Replace(" ", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }
    }
}