using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreCustomerProfileModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly CustomerManager _customerManager;
        private readonly UserManager<User> _userManager;

        public StoreCustomerProfileModel(
            StoreManager storeManager,
            CustomerManager customerManager,
            UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _customerManager = customerManager;
            _userManager = userManager;
        }

        public Store Store { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();

        public int FollowersCount { get; set; }
        public bool IsFollowing { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var store = await _storeManager.GetStoreByIdAsync(id);

            if (store == null)
                return NotFound();

            Store = store;
            Products = await _storeManager.GetStoreProductsAsync(id);
            FollowersCount = await _storeManager.GetFollowersCountAsync(id);

            IsFollowing = false;

            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                var customer =
                    await _customerManager.GetCustomerByUserIdAsync(user.Id);

                if (customer != null)
                {
                    IsFollowing =
                        await _storeManager.IsFollowingAsync(customer.CustomerID, id);
                }
            }
            Reviews = await _storeManager.GetStoreReviewsAsync(id);

            return Page();
        }

        public async Task<IActionResult> OnPostFollowAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage(new { id = storeId });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage(new { id = storeId });

            await _storeManager.FollowStoreAsync(customer.CustomerID, storeId);

            return RedirectToPage(new { id = storeId });
        }

        public async Task<IActionResult> OnPostUnfollowAsync(int storeId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage(new { id = storeId });

            var customer = await _customerManager.GetCustomerByUserIdAsync(user.Id);
            if (customer == null) return RedirectToPage(new { id = storeId });

            await _storeManager.UnfollowStoreAsync(customer.CustomerID, storeId);

            return RedirectToPage(new { id = storeId });
        }
    }
}