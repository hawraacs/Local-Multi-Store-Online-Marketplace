using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

    using global::Multi_Store.Core.Entities;
    using global::Multi_Store.Core.Interfaces;
    using global::Multi_Store.Infrastructure.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Entities;
    using Multi_Store.Core.Interfaces;
    using Multi_Store.Infrastructure.Data;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class DeleteModel : PageModel
        {
            private readonly ApplicationDbContext _context;
            private readonly ICurrentStoreService _currentStoreService;
            private readonly IWebHostEnvironment _webHostEnvironment;

            public DeleteModel(
                ApplicationDbContext context,
                ICurrentStoreService currentStoreService,
                IWebHostEnvironment webHostEnvironment)
            {
                _context = context;
                _currentStoreService = currentStoreService;
                _webHostEnvironment = webHostEnvironment;
            }

            [BindProperty]
            public Product Product { get; set; } = new();

            public string StoreName { get; set; } = string.Empty;
            public string PrimaryImageUrl { get; set; } = string.Empty;
            public bool HasOrders { get; set; } = false;

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

            // Load product with images and check for orders
            Product = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.ProductID == id && p.StoreID == store.StoreID);

                if (Product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                // Check if product has any orders
                HasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductID == id);

                // Get primary image URL
                var primaryImage = Product.Images?.FirstOrDefault(i => i.IsPrimary);
                PrimaryImageUrl = primaryImage?.ImageUrl ?? Product.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png";

                return Page();
            }

            public async Task<IActionResult> OnPostAsync(int id)
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

                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == id && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                // Check if product has orders
                bool hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductID == id);
                if (hasOrders)
                {
                    // Soft delete - just deactivate instead of deleting
                    product.IsActive = false;
                    product.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    TempData["WarningMessage"] = $"Product '{product.ProductName}' has existing orders. It has been deactivated instead of deleted.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                string productName = product.ProductName;

                // Delete physical image files
                if (product.Images != null && product.Images.Any())
                {
                    string productFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", product.ProductID.ToString());
                    if (Directory.Exists(productFolder))
                    {
                        Directory.Delete(productFolder, true);
                    }
                }

                // Remove from wishlists
                var wishlistItems = _context.Wishlists.Where(w => w.ProductID == product.ProductID);
                _context.Wishlists.RemoveRange(wishlistItems);

                // Remove from carts
                var cartItems = _context.CartItems.Where(ci => ci.ProductID == product.ProductID);
                _context.CartItems.RemoveRange(cartItems);

                // Remove product images from database
                if (product.Images != null && product.Images.Any())
                {
                    _context.ProductImages.RemoveRange(product.Images);
                }

                // Remove the product
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Product '{productName}' has been deleted successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
        }
    }