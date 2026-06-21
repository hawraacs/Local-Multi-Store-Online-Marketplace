using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly DeliveryManager _deliveryManager;

        public AdminDeliveryModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            DeliveryManager deliveryManager)
        {
            _context = context;
            _userManager = userManager;
            _deliveryManager = deliveryManager;
        }

        public List<AdminDeliveryViewModel> Deliveries { get; set; }
            = new();

        public async Task OnGetAsync()
        {
            await LoadDeliveriesAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid delivery request.";

                return RedirectToPage();
            }

            try
            {
                var result =
                    await _deliveryManager
                        .ApproveDeliveryPersonAsync(id);

                TempData["Success"] =
                    $"Delivery request approved successfully. " +
                    $"Email: {result.email} | " +
                    $"Password: {result.password}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(
            int id,
            string? reason)
        {
            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid delivery request.";

                return RedirectToPage();
            }

            try
            {
                await _deliveryManager
                    .RejectDeliveryPersonAsync(id, reason);

                TempData["Success"] =
                    "Delivery request rejected successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostActivateAsync(int id)
        {
            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid delivery person.";

                return RedirectToPage();
            }

            try
            {
                await _deliveryManager
                    .ActivateDeliveryPersonAsync(id);

                TempData["Success"] =
                    "Delivery staff activated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(int id)
        {
            if (id <= 0)
            {
                TempData["Error"] =
                    "Invalid delivery person.";

                return RedirectToPage();
            }

            try
            {
                await _deliveryManager
                    .DeactivateDeliveryPersonAsync(id);

                TempData["Success"] =
                    "Delivery staff deactivated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage();
        }

        private async Task LoadDeliveriesAsync()
        {
            var deliveryPeople =
                await _context.DeliveryPersons
                    .AsNoTracking()
                    .OrderByDescending(d =>
                        d.DeliveryPersonID)
                    .ToListAsync();

            Deliveries =
                new List<AdminDeliveryViewModel>();

            foreach (var delivery in deliveryPeople)
            {
                var deliveryUser =
                    await _userManager.FindByIdAsync(
                        delivery.UserID.ToString());

                User? requestedByUser = null;

                if (delivery.RequestedByUserID.HasValue)
                {
                    requestedByUser =
                        await _userManager.FindByIdAsync(
                            delivery.RequestedByUserID
                                .Value
                                .ToString());
                }

                Deliveries.Add(
                    new AdminDeliveryViewModel
                    {
                        DeliveryPersonID =
                            delivery.DeliveryPersonID,

                        UserID =
                            delivery.UserID,

                        RequestedByUserID =
                            delivery.RequestedByUserID,

                        FullName =
                            delivery.FullName,

                        PhoneNumber =
                            delivery.PhoneNumber,

                        Area =
                            delivery.Area,

                        VehicleType =
                            delivery.VehicleType,

                        VehicleNumber =
                            delivery.VehicleNumber,

                        DrivingLicenseNumber =
                            delivery.DrivingLicenseNumber,

                        IDProofURL =
                            delivery.IDProofURL,

                        RejectionReason =
                            delivery.RejectionReason,

                        Status =
                            delivery.Status,

                        IsActive =
                            delivery.IsActive,

                        ApprovedAt =
                            delivery.ApprovedAt,

                        User =
                            deliveryUser,

                        RequestedByUser =
                            requestedByUser
                    });
            }
        }
    }

    public class AdminDeliveryViewModel
    {
        public int DeliveryPersonID { get; set; }

        public int UserID { get; set; }

        public int? RequestedByUserID { get; set; }

        public string FullName { get; set; }
            = string.Empty;

        public string PhoneNumber { get; set; }
            = string.Empty;

        public string Area { get; set; }
            = string.Empty;

        public string VehicleType { get; set; }
            = string.Empty;

        public string VehicleNumber { get; set; }
            = string.Empty;

        public string DrivingLicenseNumber { get; set; }
            = string.Empty;

        public string? IDProofURL { get; set; }

        public string? RejectionReason { get; set; }

        public string Status { get; set; }
            = string.Empty;

        public bool IsActive { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public User? User { get; set; }

        public User? RequestedByUser { get; set; }
    }
}

