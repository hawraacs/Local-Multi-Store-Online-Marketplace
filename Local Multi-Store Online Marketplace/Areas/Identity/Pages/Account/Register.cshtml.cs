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
using System.Text.RegularExpressions;

namespace Local_Multi_Store_Online_Marketplace.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;
        private readonly OtpManager _otpManager;
        private readonly EmailotppManager _emailOtpManager;
       
        private readonly IEmailSender _emailSender;
        public RegisterModel(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    ApplicationDbContext context,
      OtpManager otpManager,
    EmailotppManager emailOtpService
   , IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _otpManager = otpManager;
            _emailOtpManager = emailOtpService;
           
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

            if (!string.IsNullOrWhiteSpace(Username))
            {
                var existingUser = await _userManager.FindByNameAsync(Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError(nameof(Username), "This username is already taken.");
                }
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }





            var user = new User
            {
                UserName = Username,
                Email = Email,
                EmailConfirmed = false,
                PhoneNumber = normalizedPhone,
                FullName = FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            await _userManager.AddToRoleAsync(user, "Customer");

            var customer = new Customer
            {
                UserID = user.Id
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            // DO NOT LOGIN USER HERE

            var emailOtp = await _otpManager.CreateOtpAsync(new OtpDto
            {
                UserId = user.Id,
                Type = "RegistrationEmail",
                Destination = user.Email,
                ExpiryMinutes = 5
            });

            await _emailSender.SendEmailAsync(
    user.Email,
    "Your OTP Code",
    $"Your OTP code is: {emailOtp.Code}"
);

            

            // TEMP STORAGE
            TempData["UserId"] = user.Id;

            return RedirectToPage("VerifyRegistrationOtp");
        }

        private static bool IsValidEmailStrict(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$";

            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }

        private static string? NormalizePhoneNumber(string countryCode, string phone)
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
                if (digits.Length < 7 || digits.Length > 8)
                    return null;
            }
            else if (countryCode == "+966")
            {
                if (digits.Length != 9)
                    return null;
            }
            else if (countryCode == "+971")
            {
                if (digits.Length != 9)
                    return null;
            }
            else if (countryCode == "+974")
            {
                if (digits.Length != 8)
                    return null;
            }
            else if (countryCode == "+965")
            {
                if (digits.Length != 8)
                    return null;
            }
            else if (countryCode == "+973")
            {
                if (digits.Length != 8)
                    return null;
            }
            else if (countryCode == "+968")
            {
                if (digits.Length != 8)
                    return null;
            }
            else if (countryCode == "+962")
            {
                if (digits.Length != 9)
                    return null;
            }
            else if (countryCode == "+20")
            {
                if (digits.Length < 10 || digits.Length > 11)
                    return null;
            }
            else if (countryCode == "+90")
            {
                if (digits.Length != 10)
                    return null;
            }
            else if (countryCode == "+33")
            {
                if (digits.Length != 9)
                    return null;
            }
            else if (countryCode == "+49")
            {
                if (digits.Length < 10 || digits.Length > 11)
                    return null;
            }
            else if (countryCode == "+44")
            {
                if (digits.Length < 10 || digits.Length > 11)
                    return null;
            }
            else if (countryCode == "+1")
            {
                if (digits.Length != 10)
                    return null;
            }
            else
            {
                return null;
            }

            return countryCode + digits;
        }
    }
}