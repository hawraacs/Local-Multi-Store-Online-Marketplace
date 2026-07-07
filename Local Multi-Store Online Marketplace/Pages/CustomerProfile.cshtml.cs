using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        public DeliveryPersonDTO Delivery { get; set; }
            = new DeliveryPersonDTO();

        public bool HasPendingDeliveryRequest { get; set; }

        public bool HasApprovedDeliveryAccount { get; set; }

        public bool HasRejectedDeliveryRequest { get; set; }

        public string DeliveryAccessMessage { get; set; }
            = string.Empty;

        public string DeliveryAccountEmail { get; set; }
            = string.Empty;

        [BindProperty]
        public string DeliveryPassword { get; set; }
            = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var loaded = await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            user.FullName =
                CustomerFullName?.Trim()
                ?? string.Empty;

            user.PhoneNumber =
                CustomerPhone?.Trim()
                ?? string.Empty;

            var result =
                await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                await LoadCustomerProfileAsync();

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }

            TempData["Success"] =
                "Profile updated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult>
            OnPostDeliveryLoginAsync()
        {
            /*
             * Get the currently logged-in Customer.
             */
            var currentCustomerUser =
                await _userManager.GetUserAsync(User);

            if (currentCustomerUser == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(DeliveryPassword))
            {
                TempData["DeliveryLoginError"] =
                    "Please enter your delivery password.";

                return RedirectToPage();
            }

            /*
             * Security:
             *
             * We do not accept a Delivery email from the HTML form.
             * The server finds the Delivery account linked to the
             * currently logged-in Customer.
             */
            var deliveryProfile =
                await _context.DeliveryPersons
                    .AsNoTracking()
                    .Where(d =>
                        d.RequestedByUserID ==
                        currentCustomerUser.Id)
                    .OrderByDescending(d =>
                        d.DeliveryPersonID)
                    .FirstOrDefaultAsync();

            if (deliveryProfile == null)
            {
                TempData["DeliveryLoginError"] =
                    "No delivery account is linked to your customer account.";

                return RedirectToPage();
            }

            var deliveryStatus =
                deliveryProfile.Status?.Trim();

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

            /*
             * UserID now points to the separate Delivery login
             * created by the Admin.
             */
            var deliveryUser =
                await _userManager.FindByIdAsync(
                    deliveryProfile.UserID.ToString());

            if (deliveryUser == null ||
                !deliveryUser.IsActive)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is unavailable.";

                return RedirectToPage();
            }

            /*
             * Confirm that the linked account has the Delivery role.
             */
            var isDelivery =
                await _userManager.IsInRoleAsync(
                    deliveryUser,
                    "Delivery");

            if (!isDelivery)
            {
                TempData["DeliveryLoginError"] =
                    "Delivery account is not configured correctly.";

                return RedirectToPage();
            }

            /*
             * Check the Delivery password.
             * Lockout protection is enabled after failed attempts.
             */
            var passwordResult =
                await _signInManager
                    .CheckPasswordSignInAsync(
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

            /*
             * Password is correct.
             * Sign out the Customer account.
             */
            await _signInManager.SignOutAsync();

            /*
             * Sign in the linked Delivery account.
             */
            await _signInManager.SignInAsync(
                deliveryUser,
                isPersistent: false);

            /*
             * Force newly created Delivery accounts
             * to replace their temporary password.
             */
            if (deliveryUser.MustChangePassword)
            {
                return LocalRedirect(
                    "/DeliveryFirstPasswordChange");
            }

            return LocalRedirect(
                "/DeliveryDashboard");
        }

        public async Task<IActionResult>
            OnPostStoreRequestAsync()
        {
            var loaded =
                await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            Store.Status = "Pending";

            TempData["Success"] =
                "Store owner request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }

        public async Task<IActionResult>
            OnPostDeliveryRequestAsync()
        {
            var loaded =
                await LoadCustomerProfileAsync();

            if (!loaded)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            Delivery.Status = "Pending";

            TempData["Success"] =
                "Delivery staff request submitted successfully. Waiting for admin approval.";

            return RedirectToPage();
        }

        private async Task<bool>
            LoadCustomerProfileAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return false;
            }

            CustomerFullName =
                !string.IsNullOrWhiteSpace(user.FullName)
                    ? user.FullName
                    : user.UserName ?? "Customer";

            CustomerEmail =
                user.Email
                ?? string.Empty;

            CustomerPhone =
                !string.IsNullOrWhiteSpace(user.PhoneNumber)
                    ? user.PhoneNumber
                    : "No phone number";

            await LoadDeliveryAccessStatusAsync(user);

            var customer =
                await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.UserID == user.Id);

            if (customer == null)
            {
                OrdersCount = 0;
                WishlistCount = 0;
                AddressesCount = 0;

                return true;
            }

            OrdersCount =
                await _context.Orders
                    .CountAsync(o =>
                        o.CustomerID ==
                        customer.CustomerID);

            WishlistCount =
                await _context.Wishlists
                    .CountAsync(w =>
                        w.CustomerID ==
                        customer.CustomerID);

            AddressesCount =
                await _context.CustomerAddresses
                    .CountAsync(a =>
                        a.CustomerID ==
                        customer.CustomerID);

            return true;
        }

        private async Task
            LoadDeliveryAccessStatusAsync(User user)
        {
            HasPendingDeliveryRequest = false;
            HasApprovedDeliveryAccount = false;
            HasRejectedDeliveryRequest = false;

            DeliveryAccessMessage = string.Empty;
            DeliveryAccountEmail = string.Empty;

            /*
             * New records:
             * RequestedByUserID stores the original Customer ID.
             *
             * Old pending records:
             * UserID may still contain the Customer ID before
             * the Admin creates the separate Delivery account.
             *
             * We never match accounts by phone number.
             */
            var deliveryRequest =
                await _context.DeliveryPersons
                    .AsNoTracking()
                    .Where(d =>
                        d.RequestedByUserID == user.Id
                        ||
                        (
                            d.RequestedByUserID == null
                            &&
                            d.UserID == user.Id
                        ))
                    .OrderByDescending(d =>
                        d.DeliveryPersonID)
                    .FirstOrDefaultAsync();

            if (deliveryRequest == null)
            {
                DeliveryAccessMessage =
                    "Submit your vehicle and license information to join our delivery fleet.";

                return;
            }

            var status =
                deliveryRequest.Status?.Trim();

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
                /*
                 * Load the separate Delivery Identity user.
                 */
                var deliveryUser =
                    await _userManager.FindByIdAsync(
                        deliveryRequest.UserID.ToString());

                if (deliveryUser == null ||
                    !deliveryUser.IsActive)
                {
                    DeliveryAccessMessage =
                        "Your delivery account is unavailable. Please contact the admin.";

                    return;
                }

                var isDelivery =
                    await _userManager.IsInRoleAsync(
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

                if (string.IsNullOrWhiteSpace(
                    DeliveryAccountEmail))
                {
                    DeliveryAccessMessage =
                        "Your delivery account email is unavailable. Please contact the admin.";

                    return;
                }

                HasApprovedDeliveryAccount = true;

                DeliveryAccessMessage =
                    "Your delivery account is approved. Enter the password provided by the admin.";

                return;
            }

            if (string.Equals(
                status,
                "Rejected",
                StringComparison.OrdinalIgnoreCase))
            {
                HasRejectedDeliveryRequest = true;

                if (!string.IsNullOrWhiteSpace(
                    deliveryRequest.RejectionReason))
                {
                    DeliveryAccessMessage =
                        $"Your delivery request was rejected: {deliveryRequest.RejectionReason}. You can submit a new request.";
                }
                else
                {
                    DeliveryAccessMessage =
                        "Your delivery request was rejected. You can submit a new request.";
                }

                return;
            }

            DeliveryAccessMessage =
                "Submit your vehicle and license information to join our delivery fleet.";
        }
    }
}