using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerAddressesModel : PageModel
    {
        private readonly CustomerManager _customerManager;

        public List<CustomerAddressDTO> Addresses { get; set; } = new();

        [BindProperty]
        public CustomerAddressDTO NewAddress { get; set; } = new();

        public CustomerAddressesModel(CustomerManager customerManager)
        {
            _customerManager = customerManager;
        }

        public async Task OnGetAsync()
        {
            int customerId = 1;

            Addresses = (await _customerManager
                .GetAddressesByCustomerIdAsync(customerId))
                .ToList();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            NewAddress.CustomerID = 1;

            await _customerManager.AddAddressAsync(
                NewAddress,
                "127.0.0.1",
                "Browser");

            return RedirectToPage();
        }
    }
}