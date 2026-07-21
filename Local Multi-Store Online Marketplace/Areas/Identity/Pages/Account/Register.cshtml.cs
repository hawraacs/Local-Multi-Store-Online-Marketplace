#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailSender _emailSender;

        private const string PendingRegSessionKey = "PendingRegistration";

        public RegisterModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
        }

        [BindProperty]
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Country code is required.")]
        public string CountryCode { get; set; } = "+961";

        [BindProperty]
        [Required(ErrorMessage = "Phone number is required.")]
        public string PhoneNumber { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        public void OnGet()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            FullName = FullName?.Trim();
            Username = Username?.Trim();
            Email = Email?.Trim().ToLower();
            CountryCode = CountryCode?.Trim();
            PhoneNumber = PhoneNumber?.Trim();

            if (!IsValidEmailStrict(Email))
            {
                ModelState.AddModelError(nameof(Email), "Please enter a valid email address like name@example.com.");
            }

            var normalizedPhone = NormalizePhoneNumber(CountryCode, PhoneNumber);

            if (normalizedPhone == null)
            {
                ModelState.AddModelError(nameof(PhoneNumber), "Please enter a valid phone number for the selected country.");
            }

            // Uniqueness pre-check (still re-checked at verification time to close the race window)
            if (!string.IsNullOrWhiteSpace(Username))
            {
                var existingUser = await _userManager.FindByNameAsync(Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError(nameof(Username), "This username is already taken.");
                }
            }

            if (!string.IsNullOrWhiteSpace(Email))
            {
                var existingEmail = await _userManager.FindByEmailAsync(Email);
                if (existingEmail != null)
                {
                    ModelState.AddModelError(nameof(Email), "This email is already registered.");
                }
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Generate a 6-digit OTP
            var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            var pending = new PendingRegistration
            {
                FullName = FullName,
                Username = Username,
                Email = Email,
                PhoneNumber = normalizedPhone,
                Password = Password, // kept only in server-side session, short TTL
                OtpCode = otpCode,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                FailedAttempts = 0
            };

            // Nothing is written to the database yet — no User, no Customer row.
            HttpContext.Session.SetString(PendingRegSessionKey, JsonSerializer.Serialize(pending));

            await _emailSender.SendEmailAsync(
                Email,
                "Your OTP Code",
                $"Your OTP code is: {otpCode}"
            );

            return RedirectToPage("VerifyRegistrationOtp");
        }

        private static bool IsValidEmailStrict(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$";

            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }

        private static string NormalizePhoneNumber(string countryCode, string phone)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(phone))
                return null;

            var digits = Regex.Replace(phone, @"\D", "");

            if (digits.StartsWith("0"))
            {
                digits = digits.Substring(1);
            }

            if (countryCode == "+961")
            {
                if (digits.Length < 7 || digits.Length > 8) return null;
            }
            else if (countryCode == "+966") { if (digits.Length != 9) return null; }
            else if (countryCode == "+971") { if (digits.Length != 9) return null; }
            else if (countryCode == "+974") { if (digits.Length != 8) return null; }
            else if (countryCode == "+965") { if (digits.Length != 8) return null; }
            else if (countryCode == "+973") { if (digits.Length != 8) return null; }
            else if (countryCode == "+968") { if (digits.Length != 8) return null; }
            else if (countryCode == "+962") { if (digits.Length != 9) return null; }
            else if (countryCode == "+20") { if (digits.Length < 10 || digits.Length > 11) return null; }
            else if (countryCode == "+90") { if (digits.Length != 10) return null; }
            else if (countryCode == "+33") { if (digits.Length != 9) return null; }
            else if (countryCode == "+49") { if (digits.Length < 10 || digits.Length > 11) return null; }
            else if (countryCode == "+44") { if (digits.Length < 10 || digits.Length > 11) return null; }
            else if (countryCode == "+1") { if (digits.Length != 10) return null; }
            else { return null; }

            return countryCode + digits;
        }
    }

    // Plain in-memory DTO, not an EF entity — lives only in Session, never persisted.
    public class PendingRegistration
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
        public string OtpCode { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public int FailedAttempts { get; set; }
    }
}