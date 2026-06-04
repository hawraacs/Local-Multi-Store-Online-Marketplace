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
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Products
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;

        public IndexModel(ApplicationDbContext context, ICurrentStoreService currentStoreService)
        {
            _context = context;
            _currentStoreService = currentStoreService;
        }

        public List<ProductListViewModel> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string StoreName { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is store owner
            if (!await _currentStoreService.IsStoreOwnerAsync())
            {
                return RedirectToPage("/Account/AccessDenied");
            }

            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                // Log the error (you can use ILogger instead of Console)
                Console.WriteLine("ERROR: Store not found for current user even though IsStoreOwnerAsync returned true.");
                TempData["ErrorMessage"] = "Your store information could not be found. Please contact support.";
                return RedirectToPage("/StoreOwner/RegisterStore");
            }

            StoreName = store.StoreName;
            ViewData["StoreName"] = store.StoreName;

            // Load categories for filter
            Categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // Build product query
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.StoreID == store.StoreID);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(p => p.ProductName.Contains(SearchTerm) ||
                                         p.Description.Contains(SearchTerm));
            }

            // Apply category filter
            if (CategoryFilter.HasValue && CategoryFilter.Value > 0)
            {
                query = query.Where(p => p.CategoryID == CategoryFilter.Value);
            }

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                switch (StatusFilter.ToLower())
                {
                    case "active":
                        query = query.Where(p => p.IsActive && p.Quantity > 0);
                        break;
                    case "inactive":
                        query = query.Where(p => !p.IsActive);
                        break;
                    case "outofstock":
                        query = query.Where(p => p.Quantity <= 0);
                        break;
                }
            }

            // Execute query and map to ViewModel
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            Products = products.Select(p => new ProductListViewModel
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                CompareAtPrice = p.CompareAtPrice,
                Quantity = p.Quantity,
                LowStockThreshold = p.LowStockThreshold,
                IsActive = p.IsActive,
                IsOutOfStock = p.Quantity <= 0,
                Rating = p.Rating,
                PrimaryImageUrl = p.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? p.Images?.FirstOrDefault()?.ImageUrl,
                CategoryName = p.Category?.CategoryName ?? "Uncategorized",
                CreatedAt = p.CreatedAt
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int productId)
        {
            var store = await _currentStoreService.GetCurrentStoreAsync();
            if (store == null)
            {
                return new JsonResult(new { success = false, message = "Store not found" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductID == productId && p.StoreID == store.StoreID);

            if (product == null)
            {
                return new JsonResult(new { success = false, message = "Product not found" });
            }

            product.IsActive = !product.IsActive;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                isActive = product.IsActive,
                message = product.IsActive ? "Product activated" : "Product deactivated"
            });
        }
    }
}