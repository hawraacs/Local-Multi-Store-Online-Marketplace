using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.Linq;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class AdminStoresModel : PageModel
    {
        private readonly StoreManager _storeManager;

        public AdminStoresModel(StoreManager storeManager)
        {
            _storeManager = storeManager;
        }

        public List<StoreDTO> Stores { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stores = (await _storeManager.GetAllStoresAsync()).ToList();
        }

        public async Task<IActionResult> OnPostApprove(int id)
        {
            await _storeManager.ApproveStoreAsync(id, 1);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReject(int id)
        {
            await _storeManager.RejectStoreAsync(id);
            return RedirectToPage();
        }
    }
}
