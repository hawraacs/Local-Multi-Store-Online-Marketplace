using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.ViewModels.StoreOwner;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        public List<Category> Categories { get; set; } = new();

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
                // ✅ Changed to existing Dashboard page
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            ViewData["StoreName"] = store.StoreName;

            Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("=== Create Product POST started ===");

            try
            {
                // Check store owner role
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    Console.WriteLine("User is not a store owner");
                    return RedirectToPage("/Account/AccessDenied");
                }

                // Get current store
                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    Console.WriteLine("Store is null - redirecting to Dashboard");
                    TempData["ErrorMessage"] = "Store not found. Please contact support.";
                    // ✅ Redirect to Dashboard (exists)
                    return RedirectToPage("/StoreOwner/Dashboard");
                }
                Console.WriteLine($"Store found: ID={store.StoreID}, Name={store.StoreName}");

                // Log incoming product data
                Console.WriteLine($"Product Name: {ProductVM?.ProductName ?? "(null)"}");
                Console.WriteLine($"CategoryID: {ProductVM?.CategoryID}");
                Console.WriteLine($"Price: {ProductVM?.Price}");
                Console.WriteLine($"Quantity: {ProductVM?.Quantity}");
                Console.WriteLine($"IsActive: {ProductVM?.IsActive}");

                // Manually validate required fields (ModelState may be empty)
                bool hasError = false;
                if (string.IsNullOrWhiteSpace(ProductVM.ProductName))
                {
                    ModelState.AddModelError("ProductVM.ProductName", "Product name is required.");
                    hasError = true;
                }
                if (ProductVM.CategoryID == 0)
                {
                    ModelState.AddModelError("ProductVM.CategoryID", "Please select a category.");
                    hasError = true;
                }
                if (ProductVM.Price <= 0)
                {
                    ModelState.AddModelError("ProductVM.Price", "Price must be greater than 0.");
                    hasError = true;
                }
                if (ProductVM.Quantity < 0)
                {
                    ModelState.AddModelError("ProductVM.Quantity", "Quantity cannot be negative.");
                    hasError = true;
                }

                if (hasError || !ModelState.IsValid)
                {
                    Console.WriteLine("ModelState is invalid.");
                    foreach (var err in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"  - {err.ErrorMessage}");
                    }
                    Categories = await _context.Categories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.DisplayOrder)
                        .ToListAsync();
                    return Page();
                }

                // Generate slug
                string productSlug = GenerateSlug(ProductVM.ProductName);
                int counter = 1;
                string originalSlug = productSlug;
                while (await _context.Products.AnyAsync(p => p.ProductSlug == productSlug && p.StoreID == store.StoreID))
                {
                    productSlug = $"{originalSlug}-{counter++}";
                }
                Console.WriteLine($"Generated slug: {productSlug}");

                // Create product entity
                var product = new Product
                {
                    StoreID = store.StoreID,
                    CategoryID = ProductVM.CategoryID,
                    ProductName = ProductVM.ProductName,
                    ProductSlug = productSlug,
                    Description = ProductVM.Description ?? string.Empty,
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
                Console.WriteLine("Product added to context, calling SaveChangesAsync...");
                int saved = await _context.SaveChangesAsync();
                Console.WriteLine($"SaveChangesAsync returned {saved}. Product ID: {product.ProductID}");

                // Save images if any
                if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                {
                    Console.WriteLine($"Saving {ProductVM.UploadedImages.Count} images");
                    await SaveProductImages(product.ProductID, ProductVM.UploadedImages);
                }

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been created successfully!";
                Console.WriteLine("Product creation successful, redirecting to Index");
                return RedirectToPage("/StoreOwner/Products/Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!! EXCEPTION: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error saving product: {ex.Message}";

                // Reload categories for the form
                Categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();
                return Page();
            }
        }

        private async Task SaveProductImages(int productId, List<IFormFile> images)
        {
            string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", productId.ToString());
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