using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class VerifyRegistrationOtpModel : PageModel
    {
        private readonly OtpManager _otpManager;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public VerifyRegistrationOtpModel(
            OtpManager otpManager,
            UserManager<User> userManager,
            SignInManager<User> signInManager)
        {
            _otpManager = otpManager;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public string EmailOtp { get; set; }

        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = TempData["UserId"];

            if (userId == null)
            {
                ErrorMessage = "Session expired. Please register again.";
                return Page();
            }

            int id = (int)userId;

            var emailOk = await _otpManager.VerifyOtpAsync(id, "RegistrationEmail", EmailOtp);

            if (!emailOk)
            {
                ErrorMessage = "Invalid OTP code. Please try again.";
                TempData["UserId"] = id;
                return Page();
            }

            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            user.EmailConfirmed = true;

            await _userManager.UpdateAsync(user);

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToPage("/Customer1");
        }
    }
}