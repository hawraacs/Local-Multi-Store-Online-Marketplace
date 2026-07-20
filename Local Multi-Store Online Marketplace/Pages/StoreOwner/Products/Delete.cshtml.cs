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
            ViewData["StoreId"] = store.StoreID;

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

            // Check if product has any orders (any status - see OnPostAsync for why)
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

            // UPDATED - Single, explicit rule (real e-commerce best practice):
            // OrderItem.ProductID -> Product is intentionally DeleteBehavior.Restrict
            // in ApplicationDbContext, and it stays that way on purpose - order
            // history must never be silently altered or lost. That means ANY
            // OrderItem referencing this product (completed, pending, cancelled, or
            // any other status) makes a hard delete impossible at the database level,
            // full stop. So the rule is: any order reference at all -> archive
            // (deactivate) instead of delete. No status-based exceptions.
            bool hasAnyOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductID == id); // UPDATED
            if (hasAnyOrders)
            {
                // Archive instead of delete. IsActive = false is the same flag every
                // other page in this project already filters on (Index's "Active" /
                // "Inactive" status filter, Edit, etc.), so this immediately hides the
                // product from customer-facing catalog/search/listing queries that
                // filter by IsActive == true, while Store Owner/Admin pages - which
                // intentionally do not filter by IsActive - continue to show it.
                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // UPDATED - clear, specific message explaining *why* it was archived
                // rather than deleted.
                TempData["SuccessMessage"] = "This product has been archived instead of permanently deleted because it is referenced by customer orders.";
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

            // Reviews: safe to remove outright here, because we already proved above
            // that no OrderItem exists for this product, so no *verified* review
            // (Review.OrderItemID set) can exist for it either - only unattached
            // product reviews are possible.
            var productReviews = await _context.Reviews
                .Where(r => r.ProductID == product.ProductID)
                .ToListAsync();
            _context.Reviews.RemoveRange(productReviews);

            // ChatMessages: clear the reference instead of deleting the message -
            // conversation history between a customer and the store should survive
            // the product being removed; only the dangling link is cleared.
            var relatedChatMessages = await _context.ChatMessages
                .Where(cm => cm.ProductID == product.ProductID)
                .ToListAsync();
            foreach (var msg in relatedChatMessages)
            {
                msg.ProductID = null;
            }

            // RecentlyViewedProduct rows: disposable browsing history, safe to remove.
            var recentlyViewed = await _context.RecentlyViewedProducts
                .Where(rv => rv.ProductID == product.ProductID)
                .ToListAsync();
            _context.RecentlyViewedProducts.RemoveRange(recentlyViewed);

            // NOTE - Not touched, and no code needed:
            // - ProductImages: DeleteBehavior.Cascade is explicitly configured in the
            //   DbContext, and they are also removed manually below (kept as-is).
            // - ExplorePost.ProductID: explicitly configured as
            //   DeleteBehavior.SetNull in the DbContext, so the database itself
            //   nulls it out automatically when the Product row is deleted.
            // - Promotion: has no ProductID column at all in this schema (it is a
            //   Store-level broadcast, see Promotion.cs), so there is nothing to clean up.

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
