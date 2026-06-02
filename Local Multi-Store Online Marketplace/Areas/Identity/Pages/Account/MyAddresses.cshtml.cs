using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Multi_Store.Pages.Customer
{
    public class MyAddressesModel : PageModel
    {
        private readonly CustomerAddressManager _addressManager;

        public MyAddressesModel(CustomerAddressManager addressManager)
        {
            _addressManager = addressManager;
        }

        public List<CustomerAddressDTO> Addresses { get; set; } = new();

        public async Task OnGetAsync()
        {
            int customerId = GetCurrentCustomerId();
            Addresses = await _addressManager.GetCustomerAddressesAsync(customerId);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _addressManager.DeleteAddressAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetDefaultAsync(int id)
        {
            int customerId = GetCurrentCustomerId();
            await _addressManager.SetDefaultAddressAsync(customerId, id);
            return RedirectToPage();
        }

        private int GetCurrentCustomerId()
        {
            return 1;
        }
    }
}