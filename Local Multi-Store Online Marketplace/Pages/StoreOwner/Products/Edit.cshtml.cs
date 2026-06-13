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

        public EditModel(ApplicationDbContext context, ICurrentStoreService currentStoreService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public ProductViewModel ProductVM { get; set; } = new();

        [BindProperty]
        public List<int> ImagesToDelete { get; set; } = new();

        public List<SelectListItem> CategoriesSelectList { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
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

            // Load categories for dropdown
            var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
            CategoriesSelectList = categories.Select(c => new SelectListItem(c.CategoryName, c.CategoryID.ToString())).ToList();

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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
                return RedirectToPage("/Account/AccessDenied");

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            if (!ModelState.IsValid)
            {
                var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
                CategoriesSelectList = categories.Select(c => new SelectListItem(c.CategoryName, c.CategoryID.ToString())).ToList();
                return Page();
            }

            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.ProductID == ProductVM.ProductID && p.StoreID == store.StoreID);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToPage("/StoreOwner/Products/Index");
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
                await DeleteImages(ImagesToDelete);

            // Upload new images
            if (ProductVM.UploadedImages != null && ProductVM.UploadedImages.Any())
                await AddNewImages(product.ProductID, ProductVM.UploadedImages);

            // Ensure at least one primary image
            await EnsurePrimaryImage(product.ProductID);

            TempData["SuccessMessage"] = $"Product '{product.ProductName}' updated successfully!";
            return RedirectToPage("/StoreOwner/Products/Index");
        }

        public async Task<IActionResult> OnPostSetPrimaryImageAsync(int imageId, int productId)
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

        private async Task DeleteImages(List<int> imageIds)
        {
            var images = await _context.ProductImages.Where(i => imageIds.Contains(i.ImageID)).ToListAsync();
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

        private string GenerateSlug(string name)
        {
            string slug = name.ToLower().Trim().Replace(" ", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }
    }
}