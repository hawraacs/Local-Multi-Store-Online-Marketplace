using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
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
        private readonly ApplicationDbContext _context;

        public AdminStoresModel(
            StoreManager storeManager,
            SubscriptionService subscriptionService,
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _storeManager = storeManager;
            _subscriptionService = subscriptionService;
            _userManager = userManager;
            _context = context;
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

        // ── Existing handlers (Approve, Reject, Activate, Deactivate) ──
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

        // ── New: Reactivate a suspended store ──
        public async Task<IActionResult> OnPostReactivateStore(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToPage();
            }

            store.Status = "Approved";
            store.SubscriptionStatus = "Active";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Store reactivated successfully.";
            return RedirectToPage();
        }

        // ── Fixed Suspend: updates both Status and SubscriptionStatus ──
        public async Task<IActionResult> OnPostSuspend(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToPage();
            }

            store.Status = "Suspended";
            store.SubscriptionStatus = "Suspended";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Store suspended.";
            return RedirectToPage();
        }

        // ── Fixed ActivateSubscription ──
        public async Task<IActionResult> OnPostActivateSubscription(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToPage();
            }

            // If the store was suspended, bring it back
            if (store.Status == "Suspended")
                store.Status = "Approved";

            store.SubscriptionStatus = "Active";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Subscription activated.";
            return RedirectToPage();
        }

        // ── Fixed Extend ──
        public async Task<IActionResult> OnPostExtend(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToPage();
            }

            var newExpiry = DateTime.UtcNow.AddDays(30);
            if (store.SubscriptionExpiryDate.HasValue && store.SubscriptionExpiryDate > DateTime.UtcNow)
                newExpiry = store.SubscriptionExpiryDate.Value.AddDays(30);

            store.SubscriptionExpiryDate = newExpiry;
            store.SubscriptionStatus = "Active";

            // If the store was suspended, reactivate it as well
            if (store.Status == "Suspended")
                store.Status = "Approved";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Subscription extended by 30 days.";
            return RedirectToPage();
        }
    }
}