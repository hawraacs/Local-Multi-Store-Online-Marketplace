using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]

    public class AdminStoresModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;

        public AdminStoresModel(StoreManager storeManager, UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
        }

        public List<StoreDTO> Stores { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stores = (await _storeManager.GetAllStoresAsync()).ToList();
        }

        public async Task<IActionResult> OnPostApprove(int id)
        {
            var result = await _storeManager.ApproveStoreWithAccountAsync(
                id,
                1,
                _userManager);

            TempData["Email"] = result.email;
            TempData["Password"] = result.password;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReject(int id)
        {
            await _storeManager.RejectStoreAsync(id);
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostActivate(int id)
        {
            var admin = await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _storeManager.ActivateStoreAsync(id, admin.Id);

            TempData["Success"] = "Store activated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivate(int id)
        {
            await _storeManager.DeactivateStoreAsync(id);

            TempData["Success"] = "Store deactivated successfully.";

            return RedirectToPage();
        }
    }
}