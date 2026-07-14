using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    public class HomeModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly CustomerManager _customerManager;
        private readonly UserManager<User> _userManager;

        public HomeModel(
            StoreManager storeManager,
            CustomerManager customerManager,
            UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _customerManager = customerManager;
            _userManager = userManager;
        }
        [BindProperty(SupportsGet = true)]
        public int? ProductId { get; set; }
        public Store Store { get; set; }
        public List<Product> Products { get; set; } = new();

        public int FollowersCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            // ?? get store of current owner
            Store = await _storeManager.GetByUserIdAsync(user.Id);

            if (Store == null)
                return RedirectToPage("/StoreOwner/Dashboard");

            Products = await _storeManager.GetStoreProductsAsync(Store.StoreID);
            FollowersCount = await _storeManager.GetFollowersCountAsync(Store.StoreID);

            return Page();
        }
        public async Task<IActionResult> OnPostDeleteReviewAsync(
    int reviewId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Login");

            await _storeManager.DeleteProductReviewAsync(
                reviewId,
                user.Id);

            return RedirectToPage();
        }
    }
}