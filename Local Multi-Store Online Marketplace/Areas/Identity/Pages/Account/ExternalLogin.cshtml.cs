#nullable disable

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly StoreManager _storeManager;
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            ApplicationDbContext context,
            StoreManager storeManager,
            DeliveryManager deliveryManager,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _storeManager = storeManager;
            _deliveryManager = deliveryManager;
            _logger = logger;
        }

        [TempData]
        public string ErrorMessage { get; set; }

        public string ProviderDisplayName { get; set; } = string.Empty;

        public string ReturnUrl { get; set; } = string.Empty;

        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public string Email { get; set; } = string.Empty;
        }

        public IActionResult OnGet()
        {
            return RedirectToPage("./Login");
        }

        public async Task<IActionResult> OnGetCallbackAsync(
            string returnUrl = null,
            string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                ErrorMessage = $"External provider error: {remoteError}";
                return RedirectToPage("./Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login");
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                var linkedUser = await _userManager.FindByLoginAsync(
                    info.LoginProvider,
                    info.ProviderKey);

                if (linkedUser == null)
                {
                    ErrorMessage = "External login user was not found.";
                    return RedirectToPage("./Login");
                }

                _logger.LogInformation(
                    "{Provider} user logged in.",
                    info.LoginProvider);

                return await RedirectByRoleAsync(linkedUser);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Google/Facebook did not return an email address.";
                return RedirectToPage("./Login");
            }

            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                var existingLogins = await _userManager.GetLoginsAsync(existingUser);

                var alreadyLinked = existingLogins.Any(login =>
                    login.LoginProvider == info.LoginProvider &&
                    login.ProviderKey == info.ProviderKey);

                if (!alreadyLinked)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(
                        existingUser,
                        info);

                    if (!addLoginResult.Succeeded)
                    {
                        ErrorMessage = string.Join(
                            " ",
                            addLoginResult.Errors.Select(e => e.Description));

                        return RedirectToPage("./Login");
                    }
                }

                await _signInManager.SignInAsync(
                    existingUser,
                    isPersistent: false);

                _logger.LogInformation(
                    "Existing user linked with {Provider}.",
                    info.LoginProvider);

                return await RedirectByRoleAsync(existingUser);
            }

            var fullName =
                info.Principal.FindFirstValue(ClaimTypes.Name)
                ?? email.Split('@')[0];

            var newUser = new User
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                PhoneNumber = string.Empty,
                IsActive = true,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(newUser);

            if (!createResult.Succeeded)
            {
                ErrorMessage = string.Join(
                    " ",
                    createResult.Errors.Select(e => e.Description));

                return RedirectToPage("./Login");
            }

            var loginResult = await _userManager.AddLoginAsync(
                newUser,
                info);

            if (!loginResult.Succeeded)
            {
                ErrorMessage = string.Join(
                    " ",
                    loginResult.Errors.Select(e => e.Description));

                return RedirectToPage("./Login");
            }

            await _userManager.AddToRoleAsync(newUser, "Customer");

            var customer = new Customer
            {
                UserID = newUser.Id,
                IsVerified = true,
                LoyaltyPoints = 0,
                CODBlocked = false
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(
                newUser,
                isPersistent: false);

            _logger.LogInformation(
                "New user created using {Provider}.",
                info.LoginProvider);

            return RedirectToPage("/Customer1");
        }

        private async Task<IActionResult> RedirectByRoleAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("StoreOwner"))
            {
                var approved = await _storeManager.IsStoreApprovedAsync(user.Id);

                if (!approved)
                {
                    await _signInManager.SignOutAsync();
                    ErrorMessage = "Your store is waiting for admin approval.";
                    return RedirectToPage("./Login");
                }
            }

            if (roles.Contains("Delivery"))
            {
                var approved = await _deliveryManager.IsDeliveryApprovedAsync(user.Id);

                if (!approved)
                {
                    await _signInManager.SignOutAsync();
                    ErrorMessage = "Your delivery account is waiting for admin approval.";
                    return RedirectToPage("./Login");
                }
            }

            if (roles.Contains("Admin"))
            {
                return RedirectToPage("/Admin1");
            }

            if (roles.Contains("StoreOwner"))
            {
                return RedirectToPage("/Store1");
            }

            if (roles.Contains("Customer"))
            {
                return RedirectToPage("/Customer1");
            }

            if (roles.Contains("Delivery"))
            {
                return RedirectToPage("/Delivery1");
            }

            return RedirectToPage("/Index");
        }
    }
}