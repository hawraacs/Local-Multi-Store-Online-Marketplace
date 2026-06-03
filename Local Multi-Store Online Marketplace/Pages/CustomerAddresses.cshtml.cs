using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerAddressesModel : PageModel
    {
        private readonly CustomerAddressManager _addressManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public CustomerAddressesModel(
            CustomerAddressManager addressManager,
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _addressManager = addressManager;
            _userManager = userManager;
            _context = context;
        }

        public List<CustomerAddressDTO> Addresses { get; set; } = new();

        [BindProperty]
        public CustomerAddressDTO NewAddress { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Addresses = await _addressManager.GetCustomerAddressesAsync(customerId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(NewAddress.AddressLine1) ||
                string.IsNullOrWhiteSpace(NewAddress.City) ||
                string.IsNullOrWhiteSpace(NewAddress.Area))
            {
                TempData["Error"] = "Address, city, and area are required.";
                Addresses = await _addressManager.GetCustomerAddressesAsync(customerId.Value);
                return Page();
            }

            var existingAddresses = await _addressManager.GetCustomerAddressesAsync(customerId.Value);

            NewAddress.CustomerID = customerId.Value;
            NewAddress.IsActive = true;

            if (!existingAddresses.Any())
            {
                NewAddress.IsDefault = true;
            }

            await _addressManager.AddAddressAsync(NewAddress);

            TempData["Success"] = "Address added successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetDefaultAsync(int addressId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _addressManager.SetDefaultAddressAsync(customerId.Value, addressId);

            TempData["Success"] = "Default address updated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int addressId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var addresses = await _addressManager.GetCustomerAddressesAsync(customerId.Value);
            var address = addresses.FirstOrDefault(a => a.AddressID == addressId);

            if (address == null)
            {
                TempData["Error"] = "Address not found.";
                return RedirectToPage();
            }

            await _addressManager.DeleteAddressAsync(addressId);

            TempData["Success"] = "Address deleted successfully.";

            return RedirectToPage();
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            return customer?.CustomerID;
        }
    }
}