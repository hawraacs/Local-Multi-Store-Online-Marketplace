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
            Stores.Count(store =>
                string.Equals(
                    store.Status?.Trim(),
                    "Pending",
                    StringComparison.OrdinalIgnoreCase));

        public async Task OnGetAsync()
        {
            try
            {
                var allStores =
                    await _storeManager.GetAllStoresAsync();

                Stores = allStores?
                    .Where(store => store != null)
                    .ToList()
                    ?? new List<StoreDTO>();
            }
            catch (Exception ex)
            {
                Stores = new List<StoreDTO>();

                TempData["Error"] =
                    $"Unable to load stores: {ex.Message}";
            }
        }

        // =====================================================
        // APPROVE
        // Creates the separate StoreOwner login.
        // =====================================================
        public async Task<IActionResult> OnPostApprove(int id)
        {
            var admin =
                await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            try
            {
                var result =
                    await _storeManager
                        .ApproveStoreWithAccountAsync(
                            id,
                            admin.Id,
                            _userManager);

                TempData["Email"] =
                    result.email;

                TempData["Password"] =
                    result.password;

                TempData["Success"] =
                    "Store approved and Store Owner account created successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while approving the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // REJECT
        // No StoreOwner account is created.
        // =====================================================
        public async Task<IActionResult> OnPostReject(int id)
        {
            try
            {
                await _storeManager
                    .RejectStoreAsync(id);

                TempData["Success"] =
                    "Store rejected successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while rejecting the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // ACTIVATE
        // Reactivates both Store and generated StoreOwner user.
        // =====================================================
        public async Task<IActionResult> OnPostActivate(int id)
        {
            var admin =
                await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            try
            {
                await _storeManager
                    .ActivateStoreAsync(
                        id,
                        admin.Id);

                TempData["Success"] =
                    "Store and Store Owner account activated successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while activating the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // DEACTIVATE
        // Disables both Store and generated StoreOwner user.
        // Customer account remains untouched.
        // =====================================================
        public async Task<IActionResult> OnPostDeactivate(int id)
        {
            try
            {
                await _storeManager
                    .DeactivateStoreAsync(id);

                TempData["Success"] =
                    "Store and Store Owner account deactivated successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while deactivating the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // REACTIVATE SUSPENDED STORE
        // =====================================================
        public async Task<IActionResult> OnPostReactivateStore(int id)
        {
            var admin =
                await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            try
            {
                await _storeManager
                    .ActivateStoreAsync(
                        id,
                        admin.Id);

                var store =
                    await _context.Stores
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (store == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                store.Status =
                    "Approved";

                store.SubscriptionStatus =
                    "Active";

                store.IsSuspended =
                    false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Store and Store Owner account reactivated successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while reactivating the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // SUSPEND
        // Suspends Store and disables StoreOwner login.
        // =====================================================
        public async Task<IActionResult> OnPostSuspend(int id)
        {
            try
            {
                var storeSnapshot =
                    await _context.Stores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (storeSnapshot == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                if (!StatusEquals(
                        storeSnapshot.Status,
                        "Suspended"))
                {
                    await _storeManager
                        .DeactivateStoreAsync(id);
                }

                var store =
                    await _context.Stores
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (store == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                store.Status =
                    "Suspended";

                store.SubscriptionStatus =
                    "Suspended";

                store.IsSuspended =
                    true;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Store suspended and Store Owner login disabled.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while suspending the store.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // ACTIVATE SUBSCRIPTION
        // =====================================================
        public async Task<IActionResult>
            OnPostActivateSubscription(int id)
        {
            var admin =
                await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            try
            {
                var storeSnapshot =
                    await _context.Stores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (storeSnapshot == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                if (StatusEquals(
                        storeSnapshot.Status,
                        "Suspended") ||
                    StatusEquals(
                        storeSnapshot.Status,
                        "Inactive"))
                {
                    await _storeManager
                        .ActivateStoreAsync(
                            id,
                            admin.Id);
                }

                var store =
                    await _context.Stores
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (store == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                store.SubscriptionStatus =
                    "Active";

                store.IsSuspended =
                    false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Subscription activated successfully.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while activating the subscription.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // EXTEND SUBSCRIPTION
        // =====================================================
        public async Task<IActionResult> OnPostExtend(int id)
        {
            var admin =
                await _userManager.GetUserAsync(User);

            if (admin == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            try
            {
                var storeSnapshot =
                    await _context.Stores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (storeSnapshot == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                if (StatusEquals(
                        storeSnapshot.Status,
                        "Suspended") ||
                    StatusEquals(
                        storeSnapshot.Status,
                        "Inactive"))
                {
                    await _storeManager
                        .ActivateStoreAsync(
                            id,
                            admin.Id);
                }

                var store =
                    await _context.Stores
                        .FirstOrDefaultAsync(s =>
                            s.StoreID == id);

                if (store == null)
                {
                    TempData["Error"] =
                        "Store not found.";

                    return RedirectToPage();
                }

                var newExpiry =
                    DateTime.UtcNow.AddDays(30);

                if (store.SubscriptionExpiryDate.HasValue &&
                    store.SubscriptionExpiryDate.Value >
                    DateTime.UtcNow)
                {
                    newExpiry =
                        store.SubscriptionExpiryDate
                            .Value
                            .AddDays(30);
                }

                store.SubscriptionExpiryDate =
                    newExpiry;

                store.SubscriptionStatus =
                    "Active";

                store.IsSuspended =
                    false;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Subscription extended by 30 days.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "An unexpected error occurred while extending the subscription.";
            }

            return RedirectToPage();
        }

        private static bool StatusEquals(
            string? value,
            string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}