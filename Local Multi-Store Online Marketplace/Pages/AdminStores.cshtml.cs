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
        public string? GeneratedEmail { get; set; }
        public string? GeneratedPassword { get; set; }
        public AdminStoresModel(StoreManager storeManager, UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _userManager = userManager;
        }

        public List<StoreDTO> Stores { get; set; } = new();

        public async Task OnGetAsync()
        {
            var stores = await _storeManager.GetAllStoresAsync();

            Stores = stores.ToList();

            // DEBUG (IMPORTANT)
            foreach (var s in Stores)
            {
                Console.WriteLine($"STORE: {s.StoreID} | {s.Status} | {s.Email}");
            }
        }

        public async Task<IActionResult> OnPostApprove(int id)
        {
            var account =
                await _storeManager.ApproveStoreWithAccountAsync(
                    id,
                    1,
                    _userManager);

            TempData["Email"] = account.email;
            TempData["Password"] = account.password;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReject(int id)
        {
            await _storeManager.RejectStoreAsync(id);
            return RedirectToPage();
        }
    }
}