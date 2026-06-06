#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Email address is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [Display(Name = "Email")]
            public string Identifier { get; set; } = string.Empty;
        }

        public void OnGet(string email = null, string identifier = null)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                Input.Identifier = email.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(identifier))
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

            var email = Input.Identifier.Trim().ToLower();

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account found.");
                return Page();
            }

            // Mark old unused OTPs as used
            var oldOtps = await _context.PasswordResetOtps
                .Where(x => x.UserID == user.Id && !x.IsUsed)
                .ToListAsync();

            foreach (var oldOtp in oldOtps)
            {
                oldOtp.IsUsed = true;
                oldOtp.UsedAt = DateTime.UtcNow;
            }

            // Generate new OTP
            var otp = RandomNumberGenerator
                .GetInt32(100000, 999999)
                .ToString();

            var resetOtp = new PasswordResetOtp
            {
                UserID = user.Id,
                DeliveryMethod = "Email",
                Target = email,
                OtpHash = HashOtp(otp, user.Id, email),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };

            await _context.PasswordResetOtps.AddAsync(resetOtp);
            await _context.SaveChangesAsync();

            try
            {
                await _emailSender.SendEmailAsync(
                    email,
                    "Your Realnest password reset code",
                    $@"
                        <div style='font-family:Arial,sans-serif;line-height:1.6;color:#111827;'>
                            <h2 style='color:#222260;'>Reset your Realnest password</h2>

                            <p>Hello,</p>

                            <p>We received a request to reset your password.</p>

                            <p>Your verification code is:</p>

                            <div style='font-size:28px;font-weight:bold;letter-spacing:6px;
                                        background:#f3f4f6;padding:15px;border-radius:10px;
                                        text-align:center;color:#222260;'>
                                {otp}
                            </div>

                            <p>This code will expire in 10 minutes.</p>

                            <p>If you did not request this, you can safely ignore this email.</p>
                        </div>
                    ");

                _logger.LogInformation(
                    "Password reset OTP email sent successfully to {Email}",
                    email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send password reset OTP email to {Email}",
                    email);

                // TEMPORARY DEBUG MESSAGE:
                // Keep this while testing Gmail SMTP so we can see the real error.
                ModelState.AddModelError(
                    string.Empty,
                    "EMAIL ERROR: " + ex.Message);

                if (ex.InnerException != null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "INNER ERROR: " + ex.InnerException.Message);
                }

                return Page();
            }

            TempData["ResetMessage"] = "A reset verification code was sent to your email address.";
            TempData["ResetTarget"] = MaskEmail(email);
            TempData["ResetIdentifier"] = email;

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        private static string HashOtp(string otp, int userId, string target)
        {
            var raw = $"{otp}:{userId}:{target}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }

        private static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                return email;
            }

            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];

            if (name.Length <= 2)
            {
                return $"{name[0]}***@{domain}";
            }

            return $"{name[0]}***{name[^1]}@{domain}";
        }
    }
}