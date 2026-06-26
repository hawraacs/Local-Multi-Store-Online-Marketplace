using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminStoresModel : PageModel
    {
        private readonly StoreManager _storeManager;
        private readonly SubscriptionService _subscriptionService;
        private readonly UserManager<User> _userManager;

        public AdminStoresModel(
            StoreManager storeManager,
            SubscriptionService subscriptionService,
            UserManager<User> userManager)
        {
            _storeManager = storeManager;
            _subscriptionService = subscriptionService;
            _userManager = userManager;
        }

        public List<StoreDTO> Stores { get; set; } = new();

        public int PendingCount =>
            Stores.Count(s =>
                s.Status != null &&
                s.Status.Trim().Equals("Pending", StringComparison.OrdinalIgnoreCase));

        public async Task OnGetAsync()
        {
            var allStores = await _storeManager.GetAllStoresAsync();
            Stores = allStores?.Where(s => s != null).ToList() ?? new();
        }

        // ================= STORE MANAGEMENT =================

        public async Task<IActionResult> OnPostApprove(int id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var result = await _storeManager.ApproveStoreWithAccountAsync(id, admin.Id, _userManager);

            TempData["Email"] = result.email;
            TempData["Password"] = result.password;
            TempData["Success"] = "Store approved successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReject(int id)
        {
            await _storeManager.RejectStoreAsync(id);
            TempData["Success"] = "Store rejected successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostActivate(int id)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

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

        // ================= SUBSCRIPTION FIX (THIS WAS MISSING) =================

        public IActionResult OnPostExtend(int id)
        {
            _subscriptionService.ExtendSubscription(id);

            TempData["Success"] = "Subscription extended by 30 days.";
            return RedirectToPage();
        }

        public IActionResult OnPostSuspend(int id)
        {
            _subscriptionService.SetStoreStatus(id, "Suspended");

            TempData["Success"] = "Subscription suspended.";
            return RedirectToPage();
        }

        public IActionResult OnPostActivateSubscription(int id)
        {
            _subscriptionService.SetStoreStatus(id, "Active");

            TempData["Success"] = "Subscription activated.";
            return RedirectToPage();
        }
    }
}