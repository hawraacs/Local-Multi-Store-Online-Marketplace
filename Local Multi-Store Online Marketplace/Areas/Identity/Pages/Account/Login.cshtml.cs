#nullable disable

using System.ComponentModel.DataAnnotations;
<<<<<<< HEAD
=======
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
>>>>>>> 56ec6fdf4ac7f423864bcf514949ec6fff61a293
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

        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

<<<<<<< HEAD
            if (!ModelState.IsValid)
                return Page();

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true
            );

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return Page();
=======
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    Input.Email,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: true
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    var user = await _userManager.FindByEmailAsync(Input.Email);

                    if (user != null)
                    {
                        if (await _userManager.IsInRoleAsync(user, "StoreOwner"))
                        {
                            if (!await _storeManager.IsStoreApprovedAsync(user.Id))
                            {
                                await _signInManager.SignOutAsync();
                                ModelState.AddModelError(string.Empty, "Your store is waiting for admin approval.");
                                return Page();
                            }
                        }

                        if (await _userManager.IsInRoleAsync(user, "Delivery"))
                        {
                            if (!await _deliveryManager.IsDeliveryApprovedAsync(user.Id))
                            {
                                await _signInManager.SignOutAsync();
                                ModelState.AddModelError("", "Your delivery account is waiting for admin approval.");
                                return Page();
                            }
                        }

                        return await RedirectUserByRoleAsync(user);
                    }

                    return RedirectToPage("/Index");
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa",
                        new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
>>>>>>> 56ec6fdf4ac7f423864bcf514949ec6fff61a293
            }

            _logger.LogInformation("User logged in.");

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return Page();
            }

            var roles = await _userManager.GetRolesAsync(user);

            // =========================
            // STORE OWNER CHECK
            // =========================
            if (roles.Contains("StoreOwner"))
            {
                var storeApproved = await _storeManager.IsStoreApprovedAsync(user.Id);

                if (!storeApproved)
                {
                    ModelState.AddModelError("", "Your store is waiting for admin approval.");
                    return Page();
                }
            }

            // =========================
            // DELIVERY CHECK
            // =========================
            if (roles.Contains("Delivery"))
            {
                var deliveryApproved = await _deliveryManager.IsDeliveryApprovedAsync(user.Id);

                if (!deliveryApproved)
                {
                    ModelState.AddModelError("", "Your delivery account is waiting for admin approval.");
                    return Page();
                }
            }

            // =========================
            // ROLE REDIRECTION
            // =========================
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

        public IActionResult OnPostExternalLogin(string provider, string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var redirectUrl = Url.Page(
                "./Login",
                pageHandler: "ExternalLoginCallback",
                values: new { returnUrl });

            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                provider,
                redirectUrl);

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetExternalLoginCallbackAsync(
            string returnUrl = null,
            string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "{Name} logged in with {LoginProvider}.",
                    info.Principal.Identity.Name,
                    info.LoginProvider);

                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var user = await _userManager.FindByEmailAsync(email);

                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "StoreOwner"))
                    {
                        if (!await _storeManager.IsStoreApprovedAsync(user.Id))
                        {
                            await _signInManager.SignOutAsync();
                            ModelState.AddModelError(string.Empty, "Your store is waiting for admin approval.");
                            return Page();
                        }
                    }

                    if (await _userManager.IsInRoleAsync(user, "Delivery"))
                    {
                        if (!await _deliveryManager.IsDeliveryApprovedAsync(user.Id))
                        {
                            await _signInManager.SignOutAsync();
                            ModelState.AddModelError("", "Your delivery account is waiting for admin approval.");
                            return Page();
                        }
                    }

                    return await RedirectUserByRoleAsync(user);
                }

                return RedirectToPage("/Index");
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            var externalEmail = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(externalEmail))
            {
                ModelState.AddModelError(string.Empty, "Email not received from external provider.");
                return Page();
            }

            var existingUser = await _userManager.FindByEmailAsync(externalEmail);

            if (existingUser == null)
            {
                existingUser = new User
                {
                    UserName = externalEmail,
                    Email = externalEmail,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(existingUser);

                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return Page();
                }

                var roleResult = await _userManager.AddToRoleAsync(existingUser, "Customer");

                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return Page();
                }
            }

            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);

            if (!addLoginResult.Succeeded)
            {
                foreach (var error in addLoginResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            await _signInManager.SignInAsync(existingUser, isPersistent: false);

            _logger.LogInformation(
                "User created or connected an account using {LoginProvider}.",
                info.LoginProvider);

            return await RedirectUserByRoleAsync(existingUser);
        }

        private async Task<IActionResult> RedirectUserByRoleAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

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