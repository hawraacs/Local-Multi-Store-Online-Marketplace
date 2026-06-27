using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class VerifyLoginOtpModel : PageModel
    {
        private readonly OtpManager _otpManager;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public VerifyLoginOtpModel(
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
            var userIdObj = TempData["LoginUserId"];

            if (userIdObj == null)
            {
                ErrorMessage = "Session expired. Please login again.";
                return Page();
            }

            int userId = int.Parse(userIdObj.ToString());

            var emailOk = await _otpManager.VerifyOtpAsync(
                userId,
                "LoginEmail",
                EmailOtp);

            if (!emailOk)
            {
                ErrorMessage = "Invalid OTP. Try again.";
                TempData["LoginUserId"] = userId;
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToPage("/Customer1");
        }
    }
}