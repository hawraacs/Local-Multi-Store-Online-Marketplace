using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AdminDeliveryModel> _logger;

        public AdminDeliveryModel(
        ApplicationDbContext context,
        UserManager<User> userManager,
        DeliveryManager deliveryManager,
        IEmailSender emailSender,
        ILogger<AdminDeliveryModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _deliveryManager = deliveryManager;
            _emailSender = emailSender;
            _logger = logger;
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
                // Load the request before DeliveryManager replaces
                // UserID with the generated Delivery account ID.
                var deliveryRequest =
                    await _context.DeliveryPersons
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d =>
                            d.DeliveryPersonID == id);

                if (deliveryRequest == null)
                {
                    TempData["Error"] =
                        "Delivery request was not found.";

                    return RedirectToPage();
                }

                var status =
                    deliveryRequest.Status?.Trim();

                if (!string.Equals(
                        status,
                        "Pending",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] =
                        "Only pending delivery requests can be approved.";

                    return RedirectToPage();
                }

                // RequestedByUserID permanently points to
                // the original Customer account.
                // UserID is a fallback for older pending requests.
                var originalCustomerUserId =
                    deliveryRequest.RequestedByUserID
                    ?? deliveryRequest.UserID;

                var originalCustomer =
                    await _userManager.FindByIdAsync(
                        originalCustomerUserId.ToString());

                if (originalCustomer == null)
                {
                    TempData["Error"] =
                        "The Customer account linked to this delivery request was not found.";

                    return RedirectToPage();
                }

                if (string.IsNullOrWhiteSpace(
                        originalCustomer.Email))
                {
                    TempData["Error"] =
                        "The Customer does not have a registered email address.";

                    return RedirectToPage();
                }

                // Creates the separate Delivery account:
                // deliveryX@gmail.com
                // Delivery@12345
                var result =
                    await _deliveryManager
                        .ApproveDeliveryPersonAsync(id);

                var safeCustomerName =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(
                            originalCustomer.FullName)
                            ? "Customer"
                            : originalCustomer.FullName.Trim());

                var safeDeliveryName =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(
                            deliveryRequest.FullName)
                            ? "Delivery Staff"
                            : deliveryRequest.FullName.Trim());

                var safeDeliveryEmail =
                    WebUtility.HtmlEncode(
                        result.email);

                var safeDeliveryPassword =
                    WebUtility.HtmlEncode(
                        result.password);

                var emailBody = $@"
<div style='background:#f3f4f6;padding:30px;
            font-family:Arial,sans-serif;color:#1f2937;'>

    <div style='max-width:620px;margin:auto;background:#ffffff;
                border:1px solid #e5e7eb;border-radius:14px;
                overflow:hidden;'>

        <div style='background:#0f172a;color:#ffffff;padding:24px;'>
            <h2 style='margin:0;'>
                Delivery Account Approved
            </h2>
        </div>

        <div style='padding:28px;line-height:1.6;'>

            <p>
                Hello <strong>{safeCustomerName}</strong>,
            </p>

            <p>
                Your delivery staff request for
                <strong>{safeDeliveryName}</strong>
                was approved successfully.
            </p>

            <p>
                A separate Delivery account was created for you.
            </p>

            <div style='background:#f8fafc;
                        border:1px solid #e2e8f0;
                        border-radius:10px;
                        padding:18px;
                        margin:22px 0;'>

                <p style='margin:0 0 14px;'>
                    <strong>Delivery Email</strong><br />
                    {safeDeliveryEmail}
                </p>

                <p style='margin:0;'>
                    <strong>Delivery Password</strong><br />
                    {safeDeliveryPassword}
                </p>

            </div>

            <p>
                Sign in to your Customer account, open your Profile,
                choose <strong>Management</strong>, then click
                <strong>Login as Delivery Staff</strong>.
            </p>

            <p style='color:#64748b;
                      font-size:13px;
                      margin-bottom:0;'>
                Keep these credentials private and do not share them.
            </p>

        </div>
    </div>
</div>";

                try
                {
                    await _emailSender.SendEmailAsync(
                        originalCustomer.Email,
                        "Your Realnest Delivery Account",
                        emailBody);

                    _logger.LogInformation(
                        "Delivery credentials were emailed to Customer {CustomerEmail} for Delivery request {DeliveryPersonId}.",
                        originalCustomer.Email,
                        id);

                    TempData["Success"] =
                        "Delivery request approved successfully. " +
                        "The Delivery email and password were sent " +
                        "to the Customer's registered email.";
                }
                catch (Exception emailException)
                {
                    _logger.LogError(
                        emailException,
                        "Delivery request {DeliveryPersonId} was approved, but sending credentials to {CustomerEmail} failed.",
                        id,
                        originalCustomer.Email);

                    TempData["Error"] =
                        "The Delivery account was created and approved, " +
                        "but the credentials email could not be sent. " +
                        "Check the SMTP settings and give the displayed " +
                        "Delivery login to the Customer manually.";
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] =
                    ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while approving Delivery request {DeliveryPersonId}.",
                    id);

                TempData["Error"] =
                    "An unexpected error occurred while approving the delivery request.";
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

