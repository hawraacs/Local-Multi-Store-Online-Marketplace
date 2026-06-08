using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerFeedModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;
        private readonly CustomerManager _customerManager;

        public CustomerFeedModel(
            StoreManager storeManager,
            UserManager<User> userManager,
            CustomerManager customerManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _customerManager = customerManager;
        }

        public List<Product> Products { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return;

            var customer =
                await _customerManager.GetCustomerByUserIdAsync(user.Id);

            if (customer == null)
                return;

            Products =
                await _storeManager.GetFeedProductsAsync(customer.CustomerID);
        }
    }
}