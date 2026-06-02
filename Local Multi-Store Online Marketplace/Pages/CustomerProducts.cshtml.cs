using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerProductsModel : PageModel
    {
        private readonly WishlistManager _wishlistManager;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;

        public CustomerProductsModel(
            WishlistManager wishlistManager,
            IProductRepository productRepository,
            ICustomerRepository customerRepository)
        {
            _wishlistManager = wishlistManager;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAddWishlistAsync(int productId)
        {
            int customerId = 1; // Temporary customer for testing

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
                    TempData["Error"] = "CustomerID 1 does not exist in the database. Create a customer first or use an existing CustomerID.";
                    return RedirectToPage();
                }

                var product = await _productRepository.GetByIdAsync(productId);

                if (product == null)
                {
                    TempData["Error"] = $"ProductID {productId} does not exist in the database. Change the productId in the page to an existing ProductID.";
                    return RedirectToPage();
                }

                if (!product.IsActive)
                {
                    TempData["Error"] = "This product is not active.";
                    return RedirectToPage();
                }

                await _wishlistManager.AddToWishlistAsync(
                    customerId: customerId,
                    productId: productId);

                TempData["Success"] = $"{product.ProductName} added to wishlist successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }
    }
}