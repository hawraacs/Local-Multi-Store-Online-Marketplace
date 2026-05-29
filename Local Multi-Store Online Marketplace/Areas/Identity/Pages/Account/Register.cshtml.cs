using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Web.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty, Required]
        public string Role { get; set; } = string.Empty;

        [BindProperty, Required]
        public string FullName { get; set; } = string.Empty;

        [BindProperty, Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [BindProperty, Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty, Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Store Owner fields
        [BindProperty]
        public string? StoreName { get; set; }

        [BindProperty]
        public string? StoreCode { get; set; }

        [BindProperty]
        public string? City { get; set; }

        [BindProperty]
        public string? Area { get; set; }

        // Delivery fields
        [BindProperty]
        public string? VehicleType { get; set; }

        [BindProperty]
        public string? VehicleNumber { get; set; }

        [BindProperty]
        public string? DrivingLicenseNumber { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = new User
            {
                UserName = Email,
                Email = Email,
                PhoneNumber = PhoneNumber,
                FullName = FullName,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            await _userManager.AddToRoleAsync(user, Role);

            if (Role == "Customer")
            {
                _context.Customers.Add(new Customer
                {
                    UserID = user.Id
                });
            }

            if (Role == "StoreOwner")
            {
                _context.Stores.Add(new Store
                {
                    OwnerUserID = user.Id,
                    StoreName = StoreName ?? "",
                    StoreCode = StoreCode ?? "",
                    City = City ?? "",
                    Area = Area ?? "",
                    Email = Email,
                    PhoneNumber = PhoneNumber,
                    Status = "Pending"
                });
            }

            if (Role == "Delivery")
            {
                _context.DeliveryPersons.Add(new DeliveryPerson
                {
                    UserID = user.Id,
                    VehicleType = VehicleType ?? "",
                    VehicleNumber = VehicleNumber ?? "",
                    DrivingLicenseNumber = DrivingLicenseNumber ?? "",
                    Status = "Pending"
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("/Account/Login");
        }
    }
}