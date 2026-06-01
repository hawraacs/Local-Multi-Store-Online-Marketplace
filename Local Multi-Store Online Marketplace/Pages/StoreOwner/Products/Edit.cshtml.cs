using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

    using global::Multi_Store.Core.Entities;
    using global::Multi_Store.Core.Interfaces;
    using global::Multi_Store.Core.ViewModels.StoreOwner;
    using global::Multi_Store.Infrastructure.Data;
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
    public class EditModel : PageModel
        {
            private readonly ApplicationDbContext _context;
            private readonly ICurrentStoreService _currentStoreService;
            private readonly IWebHostEnvironment _webHostEnvironment;

            public EditModel(
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
            public string StoreName { get; set; } = string.Empty;

            [BindProperty]
            public List<int> ImagesToDelete { get; set; } = new();

            public async Task<IActionResult> OnGetAsync(int id)
            {
                // Check if user is store owner
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    return RedirectToPage("/Account/AccessDenied");
                }

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    TempData["ErrorMessage"] = "You need to register a store first.";
                    return RedirectToPage("/StoreOwner/RegisterStore");
                }

                StoreName = store.StoreName;
            ViewData["StoreName"] = store.StoreName;

            // Load product with images
            var product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductID == id && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                // Load categories
                Categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                // Populate ViewModel
                ProductVM = new ProductViewModel
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Description = product.Description,
                    Price = product.Price,
                    CompareAtPrice = product.CompareAtPrice,
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
                    }).OrderBy(img => img.DisplayOrder).ToList(),
                    PrimaryImageUrl = product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? product.Images.FirstOrDefault()?.ImageUrl
                };

                return Page();
            }

            public async Task<IActionResult> OnPostAsync()
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    return RedirectToPage("/Account/AccessDenied");
                }

                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    return RedirectToPage("/StoreOwner/RegisterStore");
                }

                if (!ModelState.IsValid)
                {
                    Categories = await _context.Categories
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.DisplayOrder)
                        .ToListAsync();
                    return Page();
                }

                // Get existing product
                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == ProductVM.ProductID && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                // Update product slug if name changed
                if (product.ProductName != ProductVM.ProductName)
                {
                    string newSlug = GenerateSlug(ProductVM.ProductName);
                    int counter = 1;
                    string originalSlug = newSlug;
                    while (await _context.Products.AnyAsync(p => p.ProductSlug == newSlug && p.StoreID == store.StoreID && p.ProductID != product.ProductID))
                    {
                        newSlug = $"{originalSlug}-{counter++}";
                    }
                    product.ProductSlug = newSlug;
                }

                // Update product properties
                product.ProductName = ProductVM.ProductName;
                product.Description = ProductVM.Description;
                product.Price = ProductVM.Price;
                product.CompareAtPrice = ProductVM.CompareAtPrice;
                product.Quantity = ProductVM.Quantity;
                product.LowStockThreshold = ProductVM.LowStockThreshold;
                product.Weight = ProductVM.Weight;
                product.CategoryID = ProductVM.CategoryID;
                product.IsActive = ProductVM.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Handle image deletions
                if (ImagesToDelete.Any())
                {
                    await DeleteImages(ImagesToDelete);
                }

                // Handle new image uploads
                if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                {
                    await AddNewImages(product.ProductID, ProductVM.UploadedImages);
                }

                // Ensure at least one primary image exists
                await EnsurePrimaryImage(product.ProductID);

                TempData["SuccessMessage"] = $"Product '{product.ProductName}' has been updated successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }

            public async Task<IActionResult> OnPostSetPrimaryImageAsync(int imageId, int productId)
            {
                var store = await _currentStoreService.GetCurrentStoreAsync();
                if (store == null)
                {
                    return new JsonResult(new { success = false, message = "Store not found" });
                }

                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == productId && p.StoreID == store.StoreID);

                if (product == null)
                {
                    return new JsonResult(new { success = false, message = "Product not found" });
                }

                // Remove primary from all images
                foreach (var img in product.Images)
                {
                    img.IsPrimary = false;
                }

                // Set new primary
                var primaryImage = product.Images.FirstOrDefault(i => i.ImageID == imageId);
                if (primaryImage != null)
                {
                    primaryImage.IsPrimary = true;
                    await _context.SaveChangesAsync();
                    return new JsonResult(new { success = true, message = "Primary image updated" });
                }

                return new JsonResult(new { success = false, message = "Image not found" });
            }

            private async Task DeleteImages(List<int> imageIds)
            {
                var imagesToDelete = await _context.ProductImages
                    .Where(i => imageIds.Contains(i.ImageID))
                    .ToListAsync();

                foreach (var image in imagesToDelete)
                {
                    // Delete physical file
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, image.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }

                    _context.ProductImages.Remove(image);
                }

                await _context.SaveChangesAsync();
            }

            private async Task AddNewImages(int productId, List<IFormFile> images)
            {
                string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", productId.ToString());

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                // Get current max display order
                int maxOrder = await _context.ProductImages
                    .Where(i => i.ProductID == productId)
                    .Select(i => i.DisplayOrder)
                    .DefaultIfEmpty(-1)
                    .MaxAsync();

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
                            DisplayOrder = maxOrder + i + 1,
                            IsPrimary = false
                        };

                        _context.ProductImages.Add(productImage);
                    }
                }

                await _context.SaveChangesAsync();
            }

            private async Task EnsurePrimaryImage(int productId)
            {
                var images = await _context.ProductImages
                    .Where(i => i.ProductID == productId)
                    .ToListAsync();

                if (images.Any() && !images.Any(i => i.IsPrimary))
                {
                    var firstImage = images.OrderBy(i => i.DisplayOrder).First();
                    firstImage.IsPrimary = true;
                    await _context.SaveChangesAsync();
                }
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