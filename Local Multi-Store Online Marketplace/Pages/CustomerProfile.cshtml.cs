using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerProfileModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;

        public CustomerProfileModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [BindProperty]
        public string CustomerFullName { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerEmail { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerPhone { get; set; } = string.Empty;

        public int OrdersCount { get; set; }

        public int WishlistCount { get; set; }

        public int AddressesCount { get; set; }

        [BindProperty]
        public StoreDTO Store { get; set; } = new StoreDTO();

        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new DeliveryPersonDTO();

        public bool HasPendingDeliveryRequest { get; set; }

        public bool HasApprovedDeliveryAccount { get; set; }

        public bool HasRejectedDeliveryRequest { get; set; }

        public string DeliveryAccessMessage { get; set; } = string.Empty;

        public string DeliveryAccountEmail { get; set; } = string.Empty;

        [BindProperty]
        public string DeliveryPassword { get; set; } = string.Empty;

        // ==========================================
        // Instagram-style Profile Fields
        // ==========================================
        public int LoyaltyPoints { get; set; }
        public bool IsVerified { get; set; }
        public string Gender { get; set; } = "Not Specified";
        public DateTime? DateOfBirth { get; set; }
        public bool CODBlocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<StoreFollow> FollowedStoresList { get; set; } = new();
        public List<Wishlist> WishlistList { get; set; } = new();
        public List<Review> ReviewsList { get; set; } = new();

        // Safe property lookups to prevent compile-time or runtime issues with dynamic entities
        public static string GetSafeString(object? obj, string[] propertyNames, string defaultValue = "")
        {
            if (obj == null) return defaultValue;
            var type = obj.GetType();
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null) return val.ToString() ?? defaultValue;
                }
            }
            return defaultValue;
        }

        public static int GetSafeInt(object? obj, string[] propertyNames, int defaultValue = 0)
        {
            if (obj == null) return defaultValue;
            var type = obj.GetType();
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null && int.TryParse(val.ToString(), out int res)) return res;
                }
            }
            return defaultValue;
        }

        public static decimal GetSafeDecimal(object? obj, string[] propertyNames, decimal defaultValue = 0)
        {
            if (obj == null) return defaultValue;
            var type = obj.GetType();
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null && decimal.TryParse(val.ToString(), out decimal res)) return res;
                }
            }
            return defaultValue;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            user.FullName = CustomerFullName?.Trim() ?? string.Empty;
            user.PhoneNumber = CustomerPhone?.Trim() ?? string.Empty;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                await LoadCustomerProfileAsync();

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            TempData["Success"] = "Profile updated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeliveryLoginAsync()
        {
            var currentCustomerUser = await _userManager.GetUserAsync(User);

            if (currentCustomerUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(DeliveryPassword))
            {
                TempData["DeliveryLoginError"] =
                    "Please enter your delivery password.";

                return RedirectToPage();
            }

            // Security: the server selects only the Delivery account
            // that belongs to the currently signed-in Customer.
            var deliveryProfile = await _context.DeliveryPersons
                .AsNoTracking()
                .Where(d =>
                    d.RequestedByUserID == currentCustomerUser.Id)
                .OrderByDescending(d => d.DeliveryPersonID)
                .FirstOrDefaultAsync();

            if (deliveryProfile == null)
            {
                TempData["DeliveryLoginError"] =
                    "No delivery account is linked to your customer account.";

                return RedirectToPage();
            }

            var deliveryStatus = deliveryProfile.Status?.Trim();

            if (!string.Equals(
                deliveryStatus,
                "Approved",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["DeliveryLoginError"] =
                    "Your delivery account is not approved.";

                return RedirectToPage();
            }

            if (!deliveryProfile.IsActive)
            {
                TempData["DeliveryLoginError"] =
                    "Your delivery account is currently inactive.";

                return RedirectToPage();
            }

            var deliveryUser = await _userManager.FindByIdAsync(
                deliveryProfile.UserID.ToString());

            if (deliveryUser == null || !deliveryUser.IsActive)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is unavailable.";

                return RedirectToPage();
            }

            var isDelivery = await _userManager.IsInRoleAsync(
                deliveryUser,
                "Delivery");

            if (!isDelivery)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is not configured correctly.";

                return RedirectToPage();
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(
                deliveryUser,
                DeliveryPassword,
                lockoutOnFailure: true);

            if (passwordResult.IsLockedOut)
            {
                TempData["DeliveryLoginError"] =
                    "This delivery account is temporarily locked. Please try again later.";

                return RedirectToPage();
            }

            if (!passwordResult.Succeeded)
            {
                TempData["DeliveryLoginError"] =
                    "Invalid delivery credentials.";

                return RedirectToPage();
            }

            await _signInManager.SignOutAsync();

            await _signInManager.SignInAsync(
                deliveryUser,
                isPersistent: false);

            if (deliveryUser.MustChangePassword)
            {
                return LocalRedirect("/DeliveryFirstPasswordChange");
            }

            return LocalRedirect("/DeliveryDashboard");
        }

        public async Task<IActionResult> OnPostStoreRequestAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Store.Status = "Pending";

            TempData["Success"] = "Store owner request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeliveryRequestAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Delivery.Status = "Pending";

            TempData["Success"] = "Delivery staff request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }

        private async Task<bool> LoadCustomerProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            CustomerFullName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : user.UserName ?? "Customer";

            CustomerEmail = user.Email ?? string.Empty;
            CustomerPhone = user.PhoneNumber ?? "No phone number";

            await LoadDeliveryAccessStatusAsync(user);

            // Safe lookup with robust try-catches around navigation inclusions
            Customer? customer = null;
            try
            {
                customer = await _context.Customers
                    .Include(c => c.FollowedStores)
                        .ThenInclude(fs => fs.Store)
                    .Include(c => c.Wishlists)
                        .ThenInclude(w => w.Product)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.Product)
                    .FirstOrDefaultAsync(c => c.UserID == user.Id);
            }
            catch
            {
                try
                {
                    customer = await _context.Customers
                        .Include("FollowedStores")
                        .Include("Wishlists")
                        .Include("Reviews")
                        .FirstOrDefaultAsync(c => c.UserID == user.Id);
                }
                catch
                {
                    customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserID == user.Id);
                }
            }

            if (customer == null)
            {
                OrdersCount = 0;
                WishlistCount = 0;
                AddressesCount = 0;
                return true;
            }

            LoyaltyPoints = customer.LoyaltyPoints;
            IsVerified = customer.IsVerified;
            Gender = customer.Gender ?? "Not Specified";
            DateOfBirth = customer.DateOfBirth;
            CODBlocked = customer.CODBlocked;
            CreatedAt = customer.CreatedAt;

            FollowedStoresList = customer.FollowedStores?.ToList() ?? new List<StoreFollow>();
            WishlistList = customer.Wishlists?.ToList() ?? new List<Wishlist>();
            ReviewsList = customer.Reviews?.ToList() ?? new List<Review>();

            OrdersCount = await _context.Orders
                .CountAsync(o => o.CustomerID == customer.CustomerID);

            WishlistCount = WishlistList.Count;

            AddressesCount = await _context.CustomerAddresses
                .CountAsync(a => a.CustomerID == customer.CustomerID);

            return true;
        }

        private async Task LoadDeliveryAccessStatusAsync(User user)
        {
            HasPendingDeliveryRequest = false;
            HasApprovedDeliveryAccount = false;
            HasRejectedDeliveryRequest = false;
            DeliveryAccessMessage = string.Empty;
            DeliveryAccountEmail = string.Empty;

            // New records are linked through RequestedByUserID.
            // The UserID fallback is kept only for old pending requests,
            // before the Admin creates the separate Delivery login.
            var deliveryRequest = await _context.DeliveryPersons
                .AsNoTracking()
                .Where(d =>
                    d.RequestedByUserID == user.Id
                    ||
                    (
                        d.RequestedByUserID == null
                        &&
                        d.UserID == user.Id
                    ))
                .OrderByDescending(d => d.DeliveryPersonID)
                .FirstOrDefaultAsync();

            if (deliveryRequest == null)
            {
                DeliveryAccessMessage =
                    "Submit your vehicle and license information to join our delivery fleet.";

                return;
            }

            var status = deliveryRequest.Status?.Trim();

            if (string.Equals(
                status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
            {
                HasPendingDeliveryRequest = true;
                DeliveryAccessMessage =
                    "Your delivery request is pending admin approval.";

                return;
            }

            if (string.Equals(
                    status,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !deliveryRequest.IsActive)
            {
                DeliveryAccessMessage =
                    "Your delivery account is approved but currently inactive. Please contact the admin.";

                return;
            }

            if (string.Equals(
                    status,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase)
                &&
                deliveryRequest.IsActive)
            {
                var deliveryUser = await _userManager.FindByIdAsync(
                    deliveryRequest.UserID.ToString());

                if (deliveryUser == null || !deliveryUser.IsActive)
                {
                    DeliveryAccessMessage =
                        "Your delivery account is unavailable. Please contact the admin.";

                    return;
                }

                var isDelivery = await _userManager.IsInRoleAsync(
                    deliveryUser,
                    "Delivery");

                if (!isDelivery)
                {
                    DeliveryAccessMessage =
                        "Your delivery account is not configured correctly. Please contact the admin.";

                    return;
                }

                DeliveryAccountEmail =
                    deliveryUser.Email
                    ?? deliveryUser.UserName
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(DeliveryAccountEmail))
                {
                    DeliveryAccessMessage =
                        "Your delivery account email is unavailable. Please contact the admin.";

                    return;
                }

                HasApprovedDeliveryAccount = true;
                DeliveryAccessMessage =
                    "Your delivery account is approved. Enter the password provided by the administrator.";

                return;
            }

            if (string.Equals(
                status,
                "Rejected",
                StringComparison.OrdinalIgnoreCase))
            {
                HasRejectedDeliveryRequest = true;

                DeliveryAccessMessage =
                    !string.IsNullOrWhiteSpace(deliveryRequest.RejectionReason)
                        ? $"Your delivery request was rejected: {deliveryRequest.RejectionReason}. You can submit a new request."
                        : "Your delivery request was rejected. You can submit a new request.";

                return;
            }

            DeliveryAccessMessage =
                "Submit your vehicle and license information to join our delivery fleet.";
        }


    }
}