using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
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
    }
}