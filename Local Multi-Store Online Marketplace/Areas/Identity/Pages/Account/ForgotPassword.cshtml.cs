using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Multi_Store.Core.Entities;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;

        public ForgotPasswordModel(
            UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user =
                await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    code = code
                },
                protocol: Request.Scheme);

            TempData["ResetLink"] = callbackUrl;

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}