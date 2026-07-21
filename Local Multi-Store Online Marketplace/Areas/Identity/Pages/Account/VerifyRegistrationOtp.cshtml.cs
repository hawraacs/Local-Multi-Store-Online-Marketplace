using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.Text.Json;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class VerifyRegistrationOtpModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;

        private const string PendingRegSessionKey = "PendingRegistration";
        private const int MaxAttempts = 5;

        public VerifyRegistrationOtpModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [BindProperty]
        public string EmailOtp { get; set; }

        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // No pending registration in this session? Don't show a dangling verify page.
            var json = HttpContext.Session.GetString(PendingRegSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return RedirectToPage("Register");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var json = HttpContext.Session.GetString(PendingRegSessionKey);

            if (string.IsNullOrEmpty(json))
            {
                ErrorMessage = "Your session expired. Please register again.";
                return RedirectToPage("Register");
            }

            var pending = JsonSerializer.Deserialize<PendingRegistration>(json);

            if (pending == null || DateTime.UtcNow > pending.ExpiresAtUtc)
            {
                HttpContext.Session.Remove(PendingRegSessionKey);
                ErrorMessage = "Your OTP has expired. Please register again.";
                return RedirectToPage("Register");
            }

            if (pending.FailedAttempts >= MaxAttempts)
            {
                HttpContext.Session.Remove(PendingRegSessionKey);
                ErrorMessage = "Too many incorrect attempts. Please register again.";
                return RedirectToPage("Register");
            }

            if (!string.Equals(pending.OtpCode, EmailOtp?.Trim(), StringComparison.Ordinal))
            {
                pending.FailedAttempts++;
                HttpContext.Session.SetString(PendingRegSessionKey, JsonSerializer.Serialize(pending));
                ErrorMessage = $"Invalid OTP code. Please try again. ({MaxAttempts - pending.FailedAttempts} attempts left)";
                return Page();
            }

            // Re-check uniqueness in case someone else took the name/email while this was pending.
            if (await _userManager.FindByNameAsync(pending.Username) != null ||
                await _userManager.FindByEmailAsync(pending.Email) != null)
            {
                HttpContext.Session.Remove(PendingRegSessionKey);
                ErrorMessage = "That username or email was just taken. Please register again.";
                return RedirectToPage("Register");
            }

            // Only NOW does anything get written to the database.
            var user = new User
            {
                UserName = pending.Username,
                Email = pending.Email,
                EmailConfirmed = true, // verified via OTP before the row ever existed
                PhoneNumber = pending.PhoneNumber,
                FullName = pending.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, pending.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _userManager.AddToRoleAsync(user, "Customer");

            _context.Customers.Add(new Customer { UserID = user.Id });
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove(PendingRegSessionKey);

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToPage("/Customer1");
        }
    }
}