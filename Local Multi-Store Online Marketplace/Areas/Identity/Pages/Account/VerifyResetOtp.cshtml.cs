#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<VerifyResetOtpModel> _logger;

        public VerifyResetOtpModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            ILogger<VerifyResetOtpModel> logger)
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

        // ============================================================
        // EXISTING HANDLER — UNCHANGED.
        // This is the same OTP verification logic that was already here.
        // ============================================================
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

        // ============================================================
        // NEW HANDLER — added only for "Resend code".
        // Reuses the SAME OTP-generation steps and the SAME IEmailSender
        // service that ForgotPasswordModel.OnPostAsync already uses.
        // Does not touch OnPostAsync() above in any way.
        // ============================================================
        public async Task<IActionResult> OnPostResendAsync()
        {
            var email = Input?.Identifier?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email))
            {
                return new JsonResult(new { success = false, message = "Unable to send a new verification code." });
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Same "don't reveal account existence" behavior as the rest of this flow.
                return new JsonResult(new { success = false, message = "Unable to send a new verification code." });
            }

            // Same "mark old unused OTPs as used" step as ForgotPasswordModel.OnPostAsync.
            var oldOtps = await _context.PasswordResetOtps
                .Where(x => x.UserID == user.Id && x.Target == email && !x.IsUsed)
                .ToListAsync();

            foreach (var oldOtp in oldOtps)
            {
                oldOtp.IsUsed = true;
                oldOtp.UsedAt = DateTime.UtcNow;
            }

            // Same OTP generation as ForgotPasswordModel.OnPostAsync.
            var otp = RandomNumberGenerator
                .GetInt32(100000, 999999)
                .ToString();

            var resetOtp = new PasswordResetOtp
            {
                UserID = user.Id,
                DeliveryMethod = "Email",
                Target = email,
                OtpHash = HashOtp(otp, user.Id, email), // same hashing method already in this class
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };

            await _context.PasswordResetOtps.AddAsync(resetOtp);
            await _context.SaveChangesAsync();

            try
            {
                // Same IEmailSender service and same email template as ForgotPasswordModel.OnPostAsync.
                await _emailSender.SendEmailAsync(
                    email,
                    "Your Realnest password reset code",
                    $@"
                        <div style='font-family:Arial,sans-serif;line-height:1.6;color:#111827;'>
                            <h2 style='color:#222260;'>Reset your Realnest password</h2>

                            <p>Hello,</p>

                            <p>We received a request to resend your password reset code.</p>

                            <p>Your new verification code is:</p>

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
                    "Password reset OTP resend email sent successfully to {Email}",
                    email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to resend password reset OTP email to {Email}",
                    email);

                return new JsonResult(new { success = false, message = "Unable to send a new verification code." });
            }

            return new JsonResult(new { success = true, message = "New verification code sent." });
        }

        private static string HashOtp(string otp, int userId, string target)
        {
            var raw = $"{otp}:{userId}:{target}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }
    }
}
