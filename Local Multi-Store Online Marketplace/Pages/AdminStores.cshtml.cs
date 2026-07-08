using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AdminStoresModel> _logger;

        public AdminStoresModel(
        StoreManager storeManager,
        SubscriptionService subscriptionService,
        UserManager<User> userManager,
        ApplicationDbContext context,
        IEmailSender emailSender,
        ILogger<AdminStoresModel> logger)
        {
            _storeManager = storeManager;
            _subscriptionService = subscriptionService;
            _userManager = userManager;
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
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

            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid store request.";

                return RedirectToPage();
            }

            try
            {
                // Read the pending request before StoreManager changes
                // OwnerUserID to the generated StoreOwner account.
                var storeRequest =
                    await _context.Stores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(store =>
                            store.StoreID == id);

                if (storeRequest == null)
                {
                    TempData["Error"] =
                        "Store request was not found.";

                    return RedirectToPage();
                }

                // RequestedByUserID permanently points to the Customer.
                // OwnerUserID is kept as fallback for older requests.
                var originalCustomerUserId =
                    storeRequest.RequestedByUserID
                    ?? storeRequest.OwnerUserID;

                var originalCustomer =
                    await _userManager.FindByIdAsync(
                        originalCustomerUserId.ToString());

                if (originalCustomer == null)
                {
                    TempData["Error"] =
                        "The Customer account linked to this request was not found.";

                    return RedirectToPage();
                }

                if (string.IsNullOrWhiteSpace(originalCustomer.Email))
                {
                    TempData["Error"] =
                        "The Customer does not have a registered email address.";

                    return RedirectToPage();
                }

                // Create the separate StoreOwner account and approve the store.
                var result =
                    await _storeManager
                        .ApproveStoreWithAccountAsync(
                            id,
                            admin.Id,
                            _userManager);

                // Keep these for the existing Admin page.
                TempData["Email"] =
                    result.email;

                TempData["Password"] =
                    result.password;

                // An already-approved account has no recoverable plain password.
                if (string.Equals(
                        result.password,
                        "Use existing password",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] =
                        "This Store is already approved. No new password was generated or emailed.";

                    return RedirectToPage();
                }

                var safeCustomerName =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(originalCustomer.FullName)
                            ? "Customer"
                            : originalCustomer.FullName.Trim());

                var safeStoreName =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(storeRequest.StoreName)
                            ? "your store"
                            : storeRequest.StoreName.Trim());

                var safeStoreOwnerEmail =
                    WebUtility.HtmlEncode(result.email);

                var safeStoreOwnerPassword =
                    WebUtility.HtmlEncode(result.password);

                var emailBody = $@"
<div style='background:#f3f4f6;padding:30px;
            font-family:Arial,sans-serif;color:#1f2937;'>

    <div style='max-width:620px;margin:auto;background:#ffffff;
                border:1px solid #e5e7eb;border-radius:14px;
                overflow:hidden;'>

        <div style='background:#ff6b35;color:#ffffff;padding:24px;'>
            <h2 style='margin:0;'>Store Owner Account Approved</h2>
        </div>

        <div style='padding:28px;line-height:1.6;'>

            <p>Hello <strong>{safeCustomerName}</strong>,</p>

            <p>
                Your store <strong>{safeStoreName}</strong>
                was approved successfully.
            </p>

            <p>
                A separate Store Owner account was created for you.
            </p>

            <div style='background:#f8fafc;border:1px solid #e2e8f0;
                        border-radius:10px;padding:18px;margin:22px 0;'>

                <p style='margin:0 0 14px;'>
                    <strong>Store Owner Email</strong><br />
                    {safeStoreOwnerEmail}
                </p>

                <p style='margin:0;'>
                    <strong>Store Owner Password</strong><br />
                    {safeStoreOwnerPassword}
                </p>

            </div>

            <p>
                Sign in to your Customer account, open your Profile,
                choose Management, then click
                <strong>Login as Store Owner</strong>.
            </p>

            <p style='color:#64748b;font-size:13px;margin-bottom:0;'>
                Keep these credentials private and do not share them.
            </p>

        </div>
    </div>
</div>";

                try
                {
                    await _emailSender.SendEmailAsync(
                        originalCustomer.Email,
                        "Your Realnest Store Owner Account",
                        emailBody);

                    _logger.LogInformation(
                        "Store Owner credentials were emailed to Customer {CustomerEmail} for Store {StoreId}.",
                        originalCustomer.Email,
                        id);

                    TempData["Success"] =
                        "Store approved successfully. The Store Owner email and password were sent to the Customer's registered email.";
                }
                catch (Exception emailException)
                {
                    _logger.LogError(
                        emailException,
                        "Store {StoreId} was approved, but the credentials email failed for {CustomerEmail}.",
                        id,
                        originalCustomer.Email);

                    TempData["Error"] =
                        "The Store was approved and the Store Owner account was created, but the email could not be sent. Check the SMTP settings and give the displayed credentials to the Customer manually.";
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while approving Store {StoreId}.",
                    id);

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