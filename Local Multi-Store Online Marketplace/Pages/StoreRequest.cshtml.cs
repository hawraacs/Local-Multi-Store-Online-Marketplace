using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreRequestModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;

        public StoreRequestModel(
            StoreManager storeManager,
            UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
        }

        [BindProperty]
        public StoreDTO Store { get; set; } = new StoreDTO();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(Store.StoreName) ||
                string.IsNullOrWhiteSpace(Store.Description) ||
                string.IsNullOrWhiteSpace(Store.PhoneNumber) ||
                string.IsNullOrWhiteSpace(Store.Email) ||
                string.IsNullOrWhiteSpace(Store.AddressLine1) ||
                string.IsNullOrWhiteSpace(Store.City) ||
                string.IsNullOrWhiteSpace(Store.Area) ||
                string.IsNullOrWhiteSpace(Store.BusinessLicenseNumber))
            {
                TempData["Error"] = "Please fill all required store information.";
                return Page();
            }

            try
            {
                Store.OwnerUserID = user.Id;

                await _storeManager.RegisterStoreAsync(Store);

                TempData["Success"] = "Your store request has been submitted and is waiting for admin approval.";

                return RedirectToPage("/CustomerProfile");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
                return Page();
            }
        }
    }
}