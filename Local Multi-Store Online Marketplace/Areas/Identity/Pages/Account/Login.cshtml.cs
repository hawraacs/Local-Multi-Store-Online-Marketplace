#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
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
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager
                .GetExternalAuthenticationSchemesAsync())
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            // 1. LOGIN
            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
            }

            // 2. GET USER
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return Page();
            }

            var roles = await _userManager.GetRolesAsync(user);

            // 3. STORE APPROVAL CHECK
            if (roles.Contains("StoreOwner"))
            {
                var approved = await _storeManager.IsStoreApprovedAsync(user.Id);

                if (!approved)
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError("", "Your store is waiting for admin approval.");
                    return Page();
                }
            }

            // 4. DELIVERY APPROVAL CHECK
            if (roles.Contains("Delivery"))
            {
                var approved = await _deliveryManager.IsDeliveryApprovedAsync(user.Id);

                if (!approved)
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError("", "Your delivery account is waiting for admin approval.");
                    return Page();
                }
            }

            _logger.LogInformation("User logged in.");

            // 5. ROLE REDIRECTION
            if (roles.Contains("Admin"))
                return RedirectToPage("/Admin1");

            if (roles.Contains("StoreOwner"))
                return RedirectToPage("/Store1");

            if (roles.Contains("Customer"))
                return RedirectToPage("/Customer1");

            if (roles.Contains("Delivery"))
                return RedirectToPage("/Delivery1");

            return RedirectToPage("/Index");
        }
    }
}