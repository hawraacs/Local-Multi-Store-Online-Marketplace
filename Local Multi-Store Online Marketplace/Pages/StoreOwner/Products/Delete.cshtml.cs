using Microsoft.AspNetCore.Authorization;
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

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<DeleteModel> _logger;

        public DeleteModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<DeleteModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        public string StoreName { get; set; } = string.Empty;
        public string PrimaryImageUrl { get; set; } = string.Empty;

        // BUGFIX: renamed from HasOrders — it now also reflects ProductBoost records
        // (see OnPostAsync), so the confirmation page shows the right message/button
        // text regardless of *which* kind of history is forcing an archive instead
        // of a real delete.
        public bool WillArchiveInsteadOfDelete { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
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

                // BUGFIX: this used to only check OrderItems. ProductBoost is the same
                // category of "paid history that must not silently vanish" as
                // OrderItems (see the detailed reasoning in OnPostAsync) — if a boost
                // has ever been purchased for this product, it forces an archive
                // instead of a delete too, so the confirmation page needs to reflect
                // that up front rather than showing "Delete Permanently" for
                // something that will actually just be deactivated.
                bool hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductID == id);
                bool hasBoosts = await _context.ProductBoosts.AnyAsync(b => b.ProductID == id);
                WillArchiveInsteadOfDelete = hasOrders || hasBoosts;

                // Get primary image URL
                var primaryImage = Product.Images?.FirstOrDefault(i => i.IsPrimary);
                PrimaryImageUrl = primaryImage?.ImageUrl ?? Product.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png";

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Delete Product confirmation page for product {ProductId}.", id);
                TempData["ErrorMessage"] = "Something went wrong while loading this product. Please try again.";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
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

            try
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.ProductID == id && p.StoreID == store.StoreID);

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                // Single, explicit rule (real e-commerce best practice):
                // OrderItem.ProductID -> Product is intentionally DeleteBehavior.Restrict
                // in ApplicationDbContext, and it stays that way on purpose - order
                // history must never be silently altered or lost. That means ANY
                // OrderItem referencing this product (completed, pending, cancelled, or
                // any other status) makes a hard delete impossible at the database level,
                // full stop. So the rule is: any order reference at all -> archive
                // (deactivate) instead of delete. No status-based exceptions.
                //
                // BUGFIX: extended the same rule to ProductBoost. It's the same
                // category of record as OrderItems — a store owner's paid history —
                // and was previously not checked here at all, meaning a boosted
                // product could hit a hard-delete attempt, throw an FK violation,
                // and (before the other fix below) do so *after* its images had
                // already been permanently deleted from disk.
                bool hasAnyOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductID == id);
                bool hasAnyBoosts = await _context.ProductBoosts.AnyAsync(b => b.ProductID == id);

                if (hasAnyOrders || hasAnyBoosts)
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

                    var reason = hasAnyOrders && hasAnyBoosts
                        ? "it is referenced by customer orders and has boost campaign history"
                        : hasAnyOrders
                            ? "it is referenced by customer orders"
                            : "it has boost campaign history";

                    TempData["SuccessMessage"] = $"This product has been archived instead of permanently deleted because {reason}.";
                    return RedirectToPage("/StoreOwner/Products/Index");
                }

                string productName = product.ProductName;

                // BUGFIX (data-safety): removing related rows and the product itself
                // now all happen through a single SaveChangesAsync() BEFORE any
                // physical file is touched. Previously the image folder on disk was
                // deleted first, before this database transaction was known to
                // succeed — if SaveChangesAsync had thrown for any reason (e.g. an FK
                // constraint this code didn't yet account for, such as ProductBoost
                // above before this fix), EF Core would roll back every row change,
                // but the images would already be permanently gone from disk with no
                // way to get them back, leaving the product still in the database
                // with no images and no way to restore them. Deleting the files only
                // after the database has confirmed the product no longer exists means
                // a failure here can, at worst, leave orphaned files on disk — which
                // is recoverable/cleanable, unlike lost images on a surviving product.

                // Remove from wishlists
                // BUGFIX: previously passed the un-materialized IQueryable straight to
                // RemoveRange, which silently executes a *synchronous* DB call inside
                // this otherwise fully-async method. Materialized with ToListAsync()
                // first, matching the pattern already used correctly below for
                // Reviews/ChatMessages/RecentlyViewedProducts.
                var wishlistItems = await _context.Wishlists
                    .Where(w => w.ProductID == product.ProductID)
                    .ToListAsync();
                _context.Wishlists.RemoveRange(wishlistItems);

                // Remove from carts
                var cartItems = await _context.CartItems
                    .Where(ci => ci.ProductID == product.ProductID)
                    .ToListAsync();
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
                // - ProductBoost: now checked above, before we ever get here — if this
                //   line is reached, there are none for this product.

                // Remove product images from database
                var imagesToRemove = product.Images?.ToList() ?? new();
                if (imagesToRemove.Any())
                {
                    _context.ProductImages.RemoveRange(imagesToRemove);
                }

                // Remove the product
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                // Only now — after the database has confirmed the product and all its
                // rows are actually gone — do we touch the filesystem.
                if (imagesToRemove.Any())
                {
                    try
                    {
                        string productFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products", product.ProductID.ToString());
                        if (Directory.Exists(productFolder))
                        {
                            Directory.Delete(productFolder, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        // The product is already gone from the database at this point,
                        // which is what actually matters — an orphaned folder on disk
                        // is a minor, recoverable cleanup issue, not a data-loss one.
                        _logger.LogWarning(ex, "Product {ProductId} was deleted, but its image folder could not be removed from disk.", product.ProductID);
                    }
                }

                TempData["SuccessMessage"] = $"Product '{productName}' has been deleted successfully!";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}.", id);
                TempData["ErrorMessage"] = "Something went wrong while deleting the product. Please try again.";
                return RedirectToPage("/StoreOwner/Products/Index");
            }
        }
    }
}