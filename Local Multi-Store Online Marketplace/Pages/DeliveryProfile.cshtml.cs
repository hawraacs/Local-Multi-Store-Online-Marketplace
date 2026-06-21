using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public DeliveryProfileModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public DeliveryProfileViewModel Profile { get; set; } = new();

        public List<DeliveryProfileAssignmentViewModel> ActiveAssignments { get; set; } = new();

        public string? ErrorMessage { get; set; }

        [BindProperty]
        public DeliveryProfileInput Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage = "Approved delivery profile was not found.";
                return Page();
            }

            await LoadProfileAsync(deliveryPerson);

            Input = new DeliveryProfileInput
            {
                FullName = deliveryPerson.FullName,
                PhoneNumber = deliveryPerson.PhoneNumber,
                Area = deliveryPerson.Area,
                VehicleType = deliveryPerson.VehicleType,
                VehicleNumber = deliveryPerson.VehicleNumber,
                DrivingLicenseNumber = deliveryPerson.DrivingLicenseNumber
            };

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage = "Approved delivery profile was not found.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                await LoadProfileAsync(deliveryPerson);
                return Page();
            }

            var phoneExists = await _context.DeliveryPersons
                .AnyAsync(d =>
                    d.PhoneNumber == Input.PhoneNumber &&
                    d.DeliveryPersonID != deliveryPerson.DeliveryPersonID);

            if (phoneExists)
            {
                ModelState.AddModelError(string.Empty, "This phone number is already used by another delivery person.");
                await LoadProfileAsync(deliveryPerson);
                return Page();
            }

            deliveryPerson.FullName = Input.FullName.Trim();
            deliveryPerson.PhoneNumber = Input.PhoneNumber.Trim();
            deliveryPerson.Area = Input.Area.Trim();
            deliveryPerson.VehicleType = Input.VehicleType.Trim();
            deliveryPerson.VehicleNumber = Input.VehicleNumber.Trim();
            deliveryPerson.DrivingLicenseNumber = Input.DrivingLicenseNumber.Trim();

            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                user.FullName = deliveryPerson.FullName;
                user.PhoneNumber = deliveryPerson.PhoneNumber;

                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery profile updated successfully.";

            return RedirectToPage("/DeliveryProfile");
        }

        private async Task LoadProfileAsync(DeliveryPerson deliveryPerson)
        {
            var assignments = await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o.Address)
                .Where(a => a.DeliveryPersonID == deliveryPerson.DeliveryPersonID)
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            var activeAssignments = assignments
                .Where(a =>
                    a.Status != "Delivered" &&
                    a.Status != "Cancelled" &&
                    a.Status != "Failed")
                .ToList();

            var today = DateTime.UtcNow.Date;

            Profile = new DeliveryProfileViewModel
            {
                DeliveryPersonID = deliveryPerson.DeliveryPersonID,
                UserID = deliveryPerson.UserID,
                FullName = deliveryPerson.FullName,
                PhoneNumber = deliveryPerson.PhoneNumber,
                Area = deliveryPerson.Area,
                VehicleType = deliveryPerson.VehicleType,
                VehicleNumber = deliveryPerson.VehicleNumber,
                DrivingLicenseNumber = deliveryPerson.DrivingLicenseNumber,
                IDProofURL = deliveryPerson.IDProofURL,
                Status = deliveryPerson.Status,
                IsActive = deliveryPerson.IsActive,
                Rating = deliveryPerson.Rating,
                ApprovedAt = deliveryPerson.ApprovedAt,
                CurrentLatitude = deliveryPerson.CurrentLatitude,
                CurrentLongitude = deliveryPerson.CurrentLongitude,
                LastLocationUpdate = deliveryPerson.LastLocationUpdate,
                TotalAssignments = assignments.Count,
                TodayAssignments = assignments.Count(a => a.AssignedAt.Date == today),
                CompletedDeliveries = assignments.Count(a => a.Status == "Delivered"),
                ActiveDeliveries = activeAssignments.Count
            };

            ActiveAssignments = activeAssignments
                .Select(a => new DeliveryProfileAssignmentViewModel
                {
                    AssignmentID = a.AssignmentID,
                    OrderID = a.OrderID,
                    OrderNumber = a.Order != null ? a.Order.OrderNumber : "N/A",
                    OrderStatus = a.Order != null ? a.Order.Status : "N/A",
                    AssignmentStatus = a.Status,
                    AssignedAt = a.AssignedAt,
                    PickupTime = a.PickupTime,
                    DeliveryTime = a.DeliveryTime,
                    TotalAmount = a.Order != null ? a.Order.TotalAmount : 0,
                    PaymentMethod = a.Order != null ? a.Order.PaymentMethod : "N/A",
                    PaymentStatus = a.Order != null ? a.Order.PaymentStatus : "N/A",
                    CustomerAddress = a.Order != null && a.Order.Address != null
                        ? $"{a.Order.Address.AddressLine1}, {a.Order.Address.Area}, {a.Order.Address.City}"
                        : "No address"
                })
                .ToList();
        }

        private async Task<DeliveryPerson?> GetCurrentDeliveryPersonAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            return await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.UserID == user.Id &&
                    d.IsActive &&
                    d.Status == "Approved");
        }

        public string FormatLocalTime(DateTime? value)
        {
            if (!value.HasValue)
            {
                return "Not available";
            }

            var utcDate = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

            return utcDate.ToLocalTime().ToString("dd MMM yyyy - HH:mm");
        }
    }

    public class DeliveryProfileInput
    {
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Area is required.")]
        public string Area { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle type is required.")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle number is required.")]
        public string VehicleNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Driving license number is required.")]
        public string DrivingLicenseNumber { get; set; } = string.Empty;
    }

    public class DeliveryProfileViewModel
    {
        public int DeliveryPersonID { get; set; }

        public int UserID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;

        public string VehicleNumber { get; set; } = string.Empty;

        public string DrivingLicenseNumber { get; set; } = string.Empty;

        public string? IDProofURL { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public decimal Rating { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public decimal? CurrentLatitude { get; set; }

        public decimal? CurrentLongitude { get; set; }

        public DateTime? LastLocationUpdate { get; set; }

        public int TotalAssignments { get; set; }

        public int TodayAssignments { get; set; }

        public int CompletedDeliveries { get; set; }

        public int ActiveDeliveries { get; set; }
    }

    public class DeliveryProfileAssignmentViewModel
    {
        public int AssignmentID { get; set; }

        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string OrderStatus { get; set; } = string.Empty;

        public string AssignmentStatus { get; set; } = string.Empty;

        public string CustomerAddress { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; }

        public DateTime? PickupTime { get; set; }

        public DateTime? DeliveryTime { get; set; }

        public decimal TotalAmount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;
    }
}