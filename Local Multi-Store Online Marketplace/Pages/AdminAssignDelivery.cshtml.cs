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
    public class AdminDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        private const string DefaultDeliveryPassword = "Delivery@12345";

        public AdminDeliveryModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<AdminDeliveryViewModel> Deliveries { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDeliveriesAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == id);

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Delivery request not found.";
                return RedirectToPage();
            }

            try
            {
                string deliveryEmail;
                int counter = 1;

                do
                {
                    deliveryEmail = $"delivery{counter}@gmail.com";
                    counter++;
                }
                while (await _userManager.FindByEmailAsync(deliveryEmail) != null);

                var deliveryUser = new User
                {
                    UserName = deliveryEmail,
                    Email = deliveryEmail,
                    FullName = deliveryPerson.FullName,
                    PhoneNumber = deliveryPerson.PhoneNumber,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(
                    deliveryUser,
                    DefaultDeliveryPassword);

                if (!createResult.Succeeded)
                {
                    TempData["Error"] = string.Join(" | ", createResult.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }

                var roleResult = await _userManager.AddToRoleAsync(deliveryUser, "Delivery");

                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(deliveryUser);

                    TempData["Error"] = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }

                deliveryPerson.UserID = deliveryUser.Id;
                deliveryPerson.Status = "Approved";
                deliveryPerson.IsActive = true;
                deliveryPerson.ApprovedAt = DateTime.UtcNow;
                deliveryPerson.RejectionReason = null;

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"Delivery request approved successfully. Email: {deliveryEmail} | Password: {DefaultDeliveryPassword}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == id);

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Delivery request not found.";
                return RedirectToPage();
            }

            deliveryPerson.Status = "Rejected";
            deliveryPerson.IsActive = false;
            deliveryPerson.ApprovedAt = null;
            deliveryPerson.RejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "Rejected by admin."
                : reason.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery request rejected successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostActivateAsync(int id)
        {
            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == id);

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Delivery person not found.";
                return RedirectToPage();
            }

            if (deliveryPerson.Status != "Approved")
            {
                TempData["Error"] = "Only approved delivery staff can be activated.";
                return RedirectToPage();
            }

            deliveryPerson.IsActive = true;
            deliveryPerson.RejectionReason = null;

            var user = await _userManager.FindByIdAsync(deliveryPerson.UserID.ToString());

            if (user != null && !await _userManager.IsInRoleAsync(user, "Delivery"))
            {
                var roleResult = await _userManager.AddToRoleAsync(user, "Delivery");

                if (!roleResult.Succeeded)
                {
                    TempData["Error"] = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery staff activated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(int id)
        {
            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == id);

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Delivery person not found.";
                return RedirectToPage();
            }

            if (deliveryPerson.Status != "Approved")
            {
                TempData["Error"] = "Only approved delivery staff can be deactivated.";
                return RedirectToPage();
            }

            deliveryPerson.IsActive = false;

            var user = await _userManager.FindByIdAsync(deliveryPerson.UserID.ToString());

            if (user != null && await _userManager.IsInRoleAsync(user, "Delivery"))
            {
                var roleResult = await _userManager.RemoveFromRoleAsync(user, "Delivery");

                if (!roleResult.Succeeded)
                {
                    TempData["Error"] = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery staff deactivated successfully.";

            return RedirectToPage();
        }

        private async Task LoadDeliveriesAsync()
        {
            var deliveryPeople = await _context.DeliveryPersons
                .OrderByDescending(d => d.DeliveryPersonID)
                .ToListAsync();

            Deliveries = new List<AdminDeliveryViewModel>();

            foreach (var d in deliveryPeople)
            {
                var user = await _userManager.FindByIdAsync(d.UserID.ToString());

                Deliveries.Add(new AdminDeliveryViewModel
                {
                    DeliveryPersonID = d.DeliveryPersonID,
                    UserID = d.UserID,
                    FullName = d.FullName,
                    PhoneNumber = d.PhoneNumber,
                    Area = d.Area,
                    VehicleType = d.VehicleType,
                    VehicleNumber = d.VehicleNumber,
                    DrivingLicenseNumber = d.DrivingLicenseNumber,
                    IDProofURL = d.IDProofURL,
                    RejectionReason = d.RejectionReason,
                    Status = d.Status,
                    IsActive = d.IsActive,
                    ApprovedAt = d.ApprovedAt,
                    User = user
                });
            }
        }
    }

    public class AdminDeliveryViewModel
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

        public string? RejectionReason { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public User? User { get; set; }
    }
}