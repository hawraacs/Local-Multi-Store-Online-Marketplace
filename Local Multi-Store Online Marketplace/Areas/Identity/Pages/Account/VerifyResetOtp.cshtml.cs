#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class VerifyResetOtpModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public VerifyResetOtpModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string Identifier { get; set; } = string.Empty;

            [Required(ErrorMessage = "OTP is required.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
            public string Otp { get; set; } = string.Empty;
        }

        public void OnGet(string identifier = null)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                Input.Identifier = identifier.Trim();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Identifier.Trim();
            var isEmail = identifier.Contains("@");

            User? user;

            if (isEmail)
            {
                identifier = identifier.ToLower();
                user = await _userManager.FindByEmailAsync(identifier);
            }
            else
            {
                user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == identifier);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account found.");
                return Page();
            }

            var resetOtp = await _context.PasswordResetOtps
                .Where(x =>
                    x.UserID == user.Id &&
                    x.Target == identifier &&
                    !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetOtp == null)
            {
                ModelState.AddModelError(string.Empty, "OTP not found. Please request a new OTP.");
                return Page();
            }

            if (resetOtp.ExpiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "OTP expired. Please request a new OTP.");
                return Page();
            }

            var enteredHash = HashOtp(Input.Otp.Trim(), user.Id, identifier);

            if (enteredHash != resetOtp.OtpHash)
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP.");
                return Page();
            }

            resetOtp.IsUsed = true;
            resetOtp.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(resetToken));

            return RedirectToPage("./ResetPassword", new
            {
                code = encodedToken,
                email = user.Email
            });
        }

        private static string HashOtp(string otp, int userId, string target)
        {
            var raw = $"{otp}:{userId}:{target}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }
    }
}