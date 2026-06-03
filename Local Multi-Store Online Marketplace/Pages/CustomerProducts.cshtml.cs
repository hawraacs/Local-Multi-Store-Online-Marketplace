using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerProductsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly WishlistManager _wishlistManager;
        private readonly ICustomerRepository _customerRepository;

        public CustomerProductsModel(
            ApplicationDbContext context,
            WishlistManager wishlistManager,
            ICustomerRepository customerRepository)
        {
            _context = context;
            _wishlistManager = wishlistManager;
            _customerRepository = customerRepository;
        }

        public List<ProductDisplayViewModel> Products { get; set; } = new();
        public string DebugInfo { get; set; } = "";

        public async Task OnGetAsync()
        {
            // Load ALL products (no filters) for debugging
            var allProducts = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Store)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Build debug info
            DebugInfo = $"<strong>Total products in database:</strong> {allProducts.Count}<br/>";
            DebugInfo += $"<strong>Products with IsActive=true:</strong> {allProducts.Count(p => p.IsActive)}<br/>";
            DebugInfo += $"<strong>Products with Quantity>0:</strong> {allProducts.Count(p => p.Quantity > 0)}<br/>";
            DebugInfo += $"<strong>Products meeting both conditions:</strong> {allProducts.Count(p => p.IsActive && p.Quantity > 0)}<br/>";
            DebugInfo += "<hr/><strong>Product details:</strong><br/>";

            foreach (var p in allProducts)
            {
                DebugInfo += $"ID:{p.ProductID} | Name:{p.ProductName} | Active:{p.IsActive} | Qty:{p.Quantity} | Store:{p.Store?.StoreName ?? "None"} | StoreStatus:{p.Store?.Status ?? "N/A"}<br/>";
            }

            // Now load only products that should be shown (active + in stock)
            var visibleProducts = allProducts
                .Where(p => p.IsActive && p.Quantity > 0)
                .ToList();

            Products = visibleProducts.Select(p => new ProductDisplayViewModel
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                Price = p.Price,
                Description = p.Description,
                PrimaryImageUrl = p.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? p.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",
                StoreName = p.Store?.StoreName ?? "Unknown Store",
                CategoryName = p.Category?.CategoryName ?? "Uncategorized"
            }).ToList();
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            // TODO: Get actual logged‑in customer ID (this is a placeholder)
            int customerId = 1;

            try
            {
                if (productId <= 0)
                {
                    TempData["Error"] = "Invalid product selected.";
                    return RedirectToPage();
                }

                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found. Please log in again.";
                    return RedirectToPage();
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null || !product.IsActive || product.Quantity <= 0)
                {
                    TempData["Error"] = "Product is not available.";
                    return RedirectToPage();
                }

                await _wishlistManager.AddToWishlistAsync(customerId, productId);
                TempData["Success"] = $"{product.ProductName} added to your wishlist!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }
    }

    public class ProductDisplayViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PrimaryImageUrl { get; set; } = "/images/no-image.png";
        public string StoreName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}