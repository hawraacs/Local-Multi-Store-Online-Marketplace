#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminCreateStaffModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminCreateStaffModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required")]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Phone number is required")]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "Role is required")]
            public string Role { get; set; }

            [Required(ErrorMessage = "Temporary password is required")]
            [DataType(DataType.Password)]
            [Display(Name = "Temporary Password")]
            public string TemporaryPassword { get; set; } = "Temp@12345";
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

            if (Input.Role != "StoreOwner" && Input.Role != "Delivery")
            {
                ErrorMessage = "Invalid role selected.";
                return Page();
            }

            var email = Input.Email.Trim().ToLower();

            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                ErrorMessage = "A user with this email already exists.";
                return Page();
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                PhoneNumber = Input.PhoneNumber,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user, Input.TemporaryPassword);

            if (!createResult.Succeeded)
            {
                ErrorMessage = string.Join(" | ", createResult.Errors.Select(e => e.Description));
                return Page();
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Input.Role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                ErrorMessage = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                return Page();
            }

            if (Input.Role == "Delivery")
            {
                await CreateApprovedDeliveryProfileAsync(user);
            }

            if (Input.Role == "StoreOwner")
            {
                await CreateApprovedStoreProfileAsync(user);
            }

            SuccessMessage =
                $"Account created successfully. Email: {email} | Temporary Password: {Input.TemporaryPassword} | Role: {Input.Role}";

            ModelState.Clear();

            Input = new InputModel
            {
                TemporaryPassword = "Temp@12345"
            };

            return Page();
        }

        private async Task CreateApprovedDeliveryProfileAsync(User user)
        {
            var existingDeliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.UserID == user.Id);

            if (existingDeliveryPerson == null)
            {
                var deliveryPerson = new DeliveryPerson
                {
                    UserID = user.Id,
                    FullName = Input.FullName,
                    PhoneNumber = Input.PhoneNumber,
                    Area = "Default Area",
                    VehicleType = "Not Specified",
                    VehicleNumber = "Not Specified",
                    DrivingLicenseNumber = "Not Specified",
                    IDProofURL = null,
                    RejectionReason = null,
                    CurrentLatitude = null,
                    CurrentLongitude = null,
                    LastLocationUpdate = null,
                    Status = "Approved",
                    Rating = 0,
                    IsActive = true,
                    ApprovedAt = DateTime.UtcNow
                };

                _context.DeliveryPersons.Add(deliveryPerson);
            }
            else
            {
                existingDeliveryPerson.FullName = Input.FullName;
                existingDeliveryPerson.PhoneNumber = Input.PhoneNumber;
                existingDeliveryPerson.Status = "Approved";
                existingDeliveryPerson.IsActive = true;
                existingDeliveryPerson.ApprovedAt = DateTime.UtcNow;
                existingDeliveryPerson.RejectionReason = null;
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreateApprovedStoreProfileAsync(User user)
        {
            var existingStore = await _context.Stores
                .FirstOrDefaultAsync(s => s.OwnerUserID == user.Id);

            if (existingStore == null)
            {
                var store = new Store
                {
                    OwnerUserID = user.Id,
                    StoreName = $"{Input.FullName} Store",
                    StoreCode = "ST-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    Description = "Store created by admin.",
                    LogoURL = null,
                    PhoneNumber = Input.PhoneNumber,
                    Email = user.Email ?? string.Empty,
                    AddressLine1 = "Default Address",
                    AddressLine2 = null,
                    City = "Default City",
                    Area = "Default Area",
                    Latitude = 0,
                    Longitude = 0,
                    BusinessLicenseNumber = null,
                    BusinessLicenseURL = null,
                    Rating = 0,
                    TotalRatings = 0,
                    Status = "Approved",
                    CommissionRate = 10.0m,
                    CODSupported = true,
                    CODMaxLimit = 5000,
                    HasFixedDeliveryFee = false,
                    FixedDeliveryFee = null,
                    CreatedAt = DateTime.UtcNow,
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedBy = null
                };

                _context.Stores.Add(store);
            }
            else
            {
                existingStore.StoreName = string.IsNullOrWhiteSpace(existingStore.StoreName)
                    ? $"{Input.FullName} Store"
                    : existingStore.StoreName;

                existingStore.PhoneNumber = Input.PhoneNumber;
                existingStore.Email = user.Email ?? string.Empty;
                existingStore.Status = "Approved";
                existingStore.ApprovedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}