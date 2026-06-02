#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly StoreManager _storeManager;
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            StoreManager storeManager,
            DeliveryManager deliveryManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _storeManager = storeManager;
            _deliveryManager = deliveryManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; set; } = string.Empty;

        public IList<AuthenticationScheme> ExternalLogins { get; set; }
            = new List<AuthenticationScheme>();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager
                .GetExternalAuthenticationSchemesAsync())
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager
                .GetExternalAuthenticationSchemesAsync())
                .ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
<<<<<<< HEAD
=======
            {
                ModelState.AddModelError(string.Empty, "User not found.");
>>>>>>> 0809028a8c3ccd41b58c2433a1a837934c69e3dd
                return Page();

            _logger.LogInformation("User logged in.");

            return await RedirectByRoleAsync(user);
        }

        public IActionResult OnPostExternalLoginAsync(
            string provider,
            string returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return RedirectToPage("./Login");
            }

            var redirectUrl = Url.Page(
                "./ExternalLogin",
                pageHandler: "Callback",
                values: new
                {
                    returnUrl = returnUrl ?? Url.Content("~/")
                });

            var properties = _signInManager
                .ConfigureExternalAuthenticationProperties(
                    provider,
                    redirectUrl);

            return new ChallengeResult(provider, properties);
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
<<<<<<< HEAD
                    ModelState.AddModelError("", "Store not approved yet.");
=======
                    ModelState.AddModelError(
                        string.Empty,
                        "Your store is waiting for admin approval.");

>>>>>>> 0809028a8c3ccd41b58c2433a1a837934c69e3dd
                    return Page();
                }
            }

            if (roles.Contains("Delivery"))
            {
                var approved = await _deliveryManager.IsDeliveryApprovedAsync(user.Id);

                if (!approved)
                {
                    await _signInManager.SignOutAsync();
<<<<<<< HEAD
                    ModelState.AddModelError("", "Delivery not approved yet.");
=======
                    ModelState.AddModelError(
                        string.Empty,
                        "Your delivery account is waiting for admin approval.");

>>>>>>> 0809028a8c3ccd41b58c2433a1a837934c69e3dd
                    return Page();
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