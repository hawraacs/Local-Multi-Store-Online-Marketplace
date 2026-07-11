#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly StoreManager _storeManager;
        private readonly DeliveryManager _deliveryManager;
        private readonly OtpManager _otpManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            StoreManager storeManager,
            DeliveryManager deliveryManager,
            OtpManager otpManager,
            IEmailSender emailSender,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _storeManager = storeManager;
            _deliveryManager = deliveryManager;
            _otpManager = otpManager;
            _emailSender = emailSender;
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

            if (TempData["Success"] != null)
            {
                ModelState.AddModelError(string.Empty, TempData["Success"].ToString());
            }

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

            var email = Input.Email.Trim().ToLower();

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            var roles = await _userManager.GetRolesAsync(user);

            // Only Customers (and users with no elevated role) go through OTP.
            // Admin, StoreOwner, and Delivery always skip OTP, even if they
            // are mistakenly also tagged as Customer.
            var isExemptFromOtp =
                roles.Contains("Admin") ||
                roles.Contains("StoreOwner") ||
                roles.Contains("Delivery");

            if (!isExemptFromOtp && !user.EmailConfirmed)
            {
                try
                {
                    var newOtp = await _otpManager.CreateOtpAsync(new OtpDto
                    {
                        UserId = user.Id,
                        Type = "RegistrationEmail",
                        Destination = user.Email,
                        ExpiryMinutes = 5
                    });

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your OTP Code",
                        $"Your OTP code is: {newOtp.Code}");

                    TempData["UserId"] = user.Id;
                    return RedirectToPage("VerifyRegistrationOtp");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create or send OTP for {Email}", user.Email);
                    ModelState.AddModelError(string.Empty,
                        "We couldn't send your verification code right now. Please try again in a moment.");
                    return Page();
                }
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty,
                    "Account temporarily locked due to multiple failed attempts. Try again in a few minutes.");
                return Page();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

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

            if (roles.Contains("Admin")) return RedirectToPage("/Admin1");
            if (roles.Contains("StoreOwner")) return RedirectToPage("/StoreOwner/Dashboard");
            if (roles.Contains("Customer")) return RedirectToPage("/Customer1");
            if (roles.Contains("Delivery")) return RedirectToPage("/Deliverypages/DeliveryDashboard");

            return RedirectToPage("/Index");
        }
    }
}