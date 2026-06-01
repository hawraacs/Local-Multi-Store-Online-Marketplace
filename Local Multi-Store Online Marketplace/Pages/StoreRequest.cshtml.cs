using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreRequestModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;

        public StoreRequestModel(StoreManager storeManager, UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
        }

        [BindProperty]
        public StoreDTO Store { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Identity/Account/Login");

            Store.OwnerUserID = user.Id;   // ✅ REAL USER ID
            Store.Status = "Pending";

            await _storeManager.RegisterStoreAsync(Store);

            return RedirectToPage("/Customer1");
        }
    }
}