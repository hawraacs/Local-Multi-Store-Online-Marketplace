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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SmsOtpManager _smsOtpManager;

        /*
         * After the current phone is verified, changing the phone is
         * allowed for a short period only.
         */
        private const string PhoneChangeAuthorizedUntilKey =
            "PhoneChangeAuthorizedUntil";

        private static readonly HashSet<string> AllowedVehicleTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Motorcycle",
                "Car",
                "Van",
                "Bicycle"
            };

        private static readonly HashSet<string> AllowedAreas =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Beirut",
                "Mount Lebanon",
                "Tripoli",
                "Saida",
                "Tyre",
                "Zahle",
                "Bekaa",
                "Jounieh",
                "Byblos",
                "Nabatieh"
            };

        public DeliveryProfileModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            SmsOtpManager smsOtpManager)
        {
            _context = context;
            _userManager = userManager;
            _smsOtpManager = smsOtpManager;
        }

        public DeliveryProfileViewModel Profile { get; set; }
            = new();

        public List<DeliveryProfileAssignmentViewModel>
            ActiveAssignments
        { get; set; } = new();

        public string? ErrorMessage { get; set; }

        [BindProperty]
        public DeliveryProfileInput Input { get; set; }
            = new();

        /*
         * This property belongs to the separate current-phone
         * verification form, so it remains nullable during normal saves.
         */
        [BindProperty]
        public string? PhoneOtpCode { get; set; }

        public bool ShowPhoneOtpForm { get; set; }

        public bool CanEditPhoneNumber { get; set; }

        public string? PhoneMessage { get; set; }

        // =====================================================
        // GET PROFILE
        // =====================================================
        public async Task<IActionResult> OnGetAsync()
        {
            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved delivery profile was not found.";

                return Page();
            }

            CanEditPhoneNumber =
                IsPhoneChangeAuthorized();

            await LoadProfilePageAsync(deliveryPerson);

            return Page();
        }

        // =====================================================
        // UPDATE NORMAL PROFILE INFORMATION
        //
        // This handler does not update the phone number.
        // Current-phone verification is handled separately.
        // =====================================================
        public async Task<IActionResult> OnPostUpdateAsync()
        {
            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved delivery profile was not found.";

                return Page();
            }

            /*
             * The phone input becomes editable only after the current
             * phone OTP was verified. Authorization lasts ten minutes.
             */
            CanEditPhoneNumber =
                IsPhoneChangeAuthorized();

            /*
             * The OTP field belongs to a different form and must not
             * block the normal Save Changes form.
             */
            ModelState.Remove(
                nameof(PhoneOtpCode));

            Input.FullName =
                Input.FullName?.Trim()
                ?? string.Empty;

            Input.Area =
                Input.Area?.Trim()
                ?? string.Empty;

            Input.VehicleType =
                Input.VehicleType?.Trim()
                ?? string.Empty;

            Input.VehicleNumber =
                Input.VehicleNumber?.Trim()
                ?? string.Empty;

            Input.DrivingLicenseNumber =
                Input.DrivingLicenseNumber?.Trim()
                ?? string.Empty;

            var currentNormalizedPhone =
                NormalizeLebanesePhone(
                    deliveryPerson.PhoneNumber);

            var requestedNormalizedPhone =
                currentNormalizedPhone;

            if (CanEditPhoneNumber)
            {
                requestedNormalizedPhone =
                    NormalizeLebanesePhone(
                        Input.PhoneNumber);

                if (string.IsNullOrWhiteSpace(
                        requestedNormalizedPhone))
                {
                    ModelState.AddModelError(
                        "Input.PhoneNumber",
                        "Enter a valid Lebanese phone number, for example +96176123456.");
                }
                else
                {
                    /*
                     * Prevent two Delivery accounts from using the same
                     * phone number.
                     */
                    var otherDeliveryPhones =
                        await _context.DeliveryPersons
                            .AsNoTracking()
                            .Where(d =>
                                d.DeliveryPersonID !=
                                deliveryPerson.DeliveryPersonID)
                            .Select(d =>
                                d.PhoneNumber)
                            .ToListAsync();

                    var usedByAnotherDelivery =
                        otherDeliveryPhones.Any(phone =>
                            string.Equals(
                                NormalizeLebanesePhone(phone),
                                requestedNormalizedPhone,
                                StringComparison.Ordinal));

                    if (usedByAnotherDelivery)
                    {
                        ModelState.AddModelError(
                            "Input.PhoneNumber",
                            "This phone number is already used by another delivery person.");
                    }
                }
            }
            else
            {
                /*
                 * Ignore any phone value posted by the browser when the
                 * current-phone verification window is not active.
                 */
                ModelState.Remove(
                    "Input.PhoneNumber");

                Input.PhoneNumber =
                    deliveryPerson.PhoneNumber;
            }

            var isBicycle =
                string.Equals(
                    Input.VehicleType,
                    "Bicycle",
                    StringComparison.OrdinalIgnoreCase);

            var isMotorVehicle =
                string.Equals(
                    Input.VehicleType,
                    "Motorcycle",
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    Input.VehicleType,
                    "Car",
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    Input.VehicleType,
                    "Van",
                    StringComparison.OrdinalIgnoreCase);

            if (!AllowedVehicleTypes.Contains(
                    Input.VehicleType))
            {
                ModelState.AddModelError(
                    "Input.VehicleType",
                    "Please select a valid vehicle type.");
            }

            if (!AllowedAreas.Contains(
                    Input.Area))
            {
                ModelState.AddModelError(
                    "Input.Area",
                    "Please select a valid working area.");
            }

            if (isBicycle)
            {
                Input.VehicleNumber =
                    "N/A";

                Input.DrivingLicenseNumber =
                    "N/A";

                ModelState.Remove(
                    "Input.VehicleNumber");

                ModelState.Remove(
                    "Input.DrivingLicenseNumber");
            }
            else if (isMotorVehicle)
            {
                if (string.IsNullOrWhiteSpace(
                        Input.VehicleNumber))
                {
                    ModelState.AddModelError(
                        "Input.VehicleNumber",
                        "Vehicle number is required.");
                }

                if (string.IsNullOrWhiteSpace(
                        Input.DrivingLicenseNumber))
                {
                    ModelState.AddModelError(
                        "Input.DrivingLicenseNumber",
                        "Driving license number is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadProfileAsync(
                    deliveryPerson);

                if (!CanEditPhoneNumber)
                {
                    Input.PhoneNumber =
                        deliveryPerson.PhoneNumber;
                }

                return Page();
            }

            var user =
                await _userManager.GetUserAsync(
                    User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var phoneChanged =
                CanEditPhoneNumber
                &&
                !string.Equals(
                    requestedNormalizedPhone,
                    currentNormalizedPhone,
                    StringComparison.Ordinal);

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                user.FullName =
                    Input.FullName;

                if (phoneChanged)
                {
                    user.PhoneNumber =
                        requestedNormalizedPhone;

                    /*
                     * The old phone was verified, but the new phone did
                     * not receive a second OTP in this simpler flow.
                     */
                    user.PhoneNumberConfirmed =
                        false;
                }

                var updateUserResult =
                    await _userManager.UpdateAsync(
                        user);

                if (!updateUserResult.Succeeded)
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        string.Empty,
                        GetIdentityErrors(
                            updateUserResult));

                    await LoadProfileAsync(
                        deliveryPerson);

                    return Page();
                }

                deliveryPerson.FullName =
                    Input.FullName;

                deliveryPerson.Area =
                    Input.Area;

                deliveryPerson.VehicleType =
                    Input.VehicleType;

                deliveryPerson.VehicleNumber =
                    Input.VehicleNumber;

                deliveryPerson.DrivingLicenseNumber =
                    Input.DrivingLicenseNumber;

                if (phoneChanged)
                {
                    deliveryPerson.PhoneNumber =
                        requestedNormalizedPhone;
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                if (phoneChanged)
                {
                    ClearPhoneChangeAuthorization();

                    TempData["Success"] =
                        "Delivery profile and phone number updated successfully.";
                }
                else
                {
                    TempData["Success"] =
                        "Delivery profile updated successfully.";
                }

                return RedirectToPage(
                    "/DeliveryProfile");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "The delivery profile could not be updated. " +
                    ex.Message);

                await LoadProfileAsync(
                    deliveryPerson);

                return Page();
            }
        }


        // =====================================================
        // STAGE 1: SEND OTP TO CURRENT PHONE NUMBER
        // =====================================================
        public async Task<IActionResult>
            OnPostSendPhoneOtpAsync()
        {
            ModelState.Clear();

            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved delivery profile was not found.";

                return Page();
            }

            var normalizedPhone =
                NormalizeLebanesePhone(
                    deliveryPerson.PhoneNumber);

            if (string.IsNullOrWhiteSpace(
                    normalizedPhone))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The current phone number is invalid.");

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }

            try
            {
                await _smsOtpManager.SendOtpAsync(
                    normalizedPhone);

                ShowPhoneOtpForm =
                    true;

                PhoneMessage =
                    "A verification code was sent to your current phone number.";

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The verification code could not be sent. " +
                    ex.Message);

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }
        }

        // =====================================================
        // STAGE 1: VERIFY CURRENT PHONE NUMBER
        // =====================================================
        public async Task<IActionResult>
            OnPostVerifyPhoneOtpAsync()
        {
            ModelState.Clear();

            var deliveryPerson =
                await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage =
                    "Approved delivery profile was not found.";

                return Page();
            }

            var normalizedPhone =
                NormalizeLebanesePhone(
                    deliveryPerson.PhoneNumber);

            if (string.IsNullOrWhiteSpace(
                    normalizedPhone))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The current phone number is invalid.");

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }

            if (string.IsNullOrWhiteSpace(
                    PhoneOtpCode))
            {
                ModelState.AddModelError(
                    nameof(PhoneOtpCode),
                    "Enter the OTP code.");

                ShowPhoneOtpForm =
                    true;

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }

            try
            {
                var otpApproved =
                    await _smsOtpManager.VerifyOtpAsync(
                        normalizedPhone,
                        PhoneOtpCode.Trim());

                if (!otpApproved)
                {
                    ModelState.AddModelError(
                        nameof(PhoneOtpCode),
                        "The OTP code is invalid or expired.");

                    ShowPhoneOtpForm =
                        true;

                    await LoadProfilePageAsync(
                        deliveryPerson);

                    return Page();
                }

                /*
                 * Current phone ownership was confirmed.
                 * Allow entry of a new phone number for ten minutes.
                 */
                AuthorizePhoneChange();

                TempData["Success"] =
                    "Current phone verified. Edit the Current Phone Number field and click Save Changes.";

                return RedirectToPage(
                    "/DeliveryProfile");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The current phone could not be verified. " +
                    ex.Message);

                ShowPhoneOtpForm =
                    true;

                await LoadProfilePageAsync(
                    deliveryPerson);

                return Page();
            }
        }

        // =====================================================
        // LOAD COMPLETE PROFILE PAGE
        // =====================================================
        private async Task LoadProfilePageAsync(
            DeliveryPerson deliveryPerson)
        {
            await LoadProfileAsync(
                deliveryPerson);

            Input = new DeliveryProfileInput
            {
                FullName =
                    deliveryPerson.FullName,

                PhoneNumber =
                    deliveryPerson.PhoneNumber,

                Area =
                    deliveryPerson.Area,

                VehicleType =
                    deliveryPerson.VehicleType,

                VehicleNumber =
                    deliveryPerson.VehicleNumber,

                DrivingLicenseNumber =
                    deliveryPerson.DrivingLicenseNumber
            };
        }

        // =====================================================
        // LOAD PROFILE AND ASSIGNMENTS
        // =====================================================
        private async Task LoadProfileAsync(
            DeliveryPerson deliveryPerson)
        {
            var assignments =
                await _context.DeliveryAssignments
                    .Include(a =>
                        a.Order)
                    .ThenInclude(o =>
                        o.Address)
                    .Where(a =>
                        a.DeliveryPersonID ==
                        deliveryPerson.DeliveryPersonID)
                    .OrderByDescending(a =>
                        a.AssignedAt)
                    .ToListAsync();

            var activeAssignments =
                assignments
                    .Where(a =>
                        !StatusEquals(
                            a.Status,
                            "Delivered")
                        &&
                        !StatusEquals(
                            a.Status,
                            "Cancelled")
                        &&
                        !StatusEquals(
                            a.Status,
                            "Failed"))
                    .ToList();

            var today =
                DateTime.UtcNow.Date;

            Profile =
                new DeliveryProfileViewModel
                {
                    DeliveryPersonID =
                        deliveryPerson.DeliveryPersonID,

                    UserID =
                        deliveryPerson.UserID,

                    FullName =
                        deliveryPerson.FullName,

                    PhoneNumber =
                        deliveryPerson.PhoneNumber,

                    Area =
                        deliveryPerson.Area,

                    VehicleType =
                        deliveryPerson.VehicleType,

                    VehicleNumber =
                        deliveryPerson.VehicleNumber,

                    DrivingLicenseNumber =
                        deliveryPerson.DrivingLicenseNumber,

                    IDProofURL =
                        deliveryPerson.IDProofURL,

                    Status =
                        deliveryPerson.Status,

                    IsActive =
                        deliveryPerson.IsActive,

                    Rating =
                        deliveryPerson.Rating,

                    ApprovedAt =
                        deliveryPerson.ApprovedAt,

                    CurrentLatitude =
                        deliveryPerson.CurrentLatitude,

                    CurrentLongitude =
                        deliveryPerson.CurrentLongitude,

                    LastLocationUpdate =
                        deliveryPerson.LastLocationUpdate,

                    TotalAssignments =
                        assignments.Count,

                    TodayAssignments =
                        assignments.Count(a =>
                            a.AssignedAt.Date ==
                            today),

                    CompletedDeliveries =
                        assignments.Count(a =>
                            StatusEquals(
                                a.Status,
                                "Delivered")),

                    ActiveDeliveries =
                        activeAssignments.Count
                };

            ActiveAssignments =
                activeAssignments
                    .Select(a =>
                        new DeliveryProfileAssignmentViewModel
                        {
                            AssignmentID =
                                a.AssignmentID,

                            OrderID =
                                a.OrderID,

                            OrderNumber =
                                a.Order != null
                                    ? a.Order.OrderNumber
                                    : "N/A",

                            OrderStatus =
                                a.Order != null
                                    ? a.Order.Status
                                    : "N/A",

                            AssignmentStatus =
                                a.Status,

                            CustomerAddress =
                                a.Order != null &&
                                a.Order.Address != null
                                    ? $"{a.Order.Address.AddressLine1}, " +
                                      $"{a.Order.Address.Area}, " +
                                      $"{a.Order.Address.City}"
                                    : "No address",

                            AssignedAt =
                                a.AssignedAt,

                            PickupTime =
                                a.PickupTime,

                            DeliveryTime =
                                a.DeliveryTime,

                            TotalAmount =
                                a.Order != null
                                    ? a.Order.TotalAmount
                                    : 0,

                            PaymentMethod =
                                a.Order != null
                                    ? a.Order.PaymentMethod
                                    : "N/A",

                            PaymentStatus =
                                a.Order != null
                                    ? a.Order.PaymentStatus
                                    : "N/A"
                        })
                    .ToList();
        }

        // =====================================================
        // GET LOGGED-IN DELIVERY
        // =====================================================
        private async Task<DeliveryPerson?>
            GetCurrentDeliveryPersonAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            return await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.UserID ==
                    user.Id
                    &&
                    d.IsActive
                    &&
                    d.Status ==
                    "Approved");
        }

        // =====================================================
        // TEMPORARY PHONE-CHANGE AUTHORIZATION
        // =====================================================
        private void AuthorizePhoneChange()
        {
            var expiresAt =
                DateTimeOffset.UtcNow
                    .AddMinutes(10)
                    .ToUnixTimeSeconds();

            TempData[PhoneChangeAuthorizedUntilKey] =
                expiresAt.ToString();
        }

        private bool IsPhoneChangeAuthorized()
        {
            var rawValue =
                TempData.Peek(
                    PhoneChangeAuthorizedUntilKey)
                    ?.ToString();

            if (!long.TryParse(
                    rawValue,
                    out var expiresAtUnix))
            {
                return false;
            }

            var expiresAt =
                DateTimeOffset.FromUnixTimeSeconds(
                    expiresAtUnix);

            if (DateTimeOffset.UtcNow <= expiresAt)
            {
                return true;
            }

            ClearPhoneChangeAuthorization();

            return false;
        }

        private void ClearPhoneChangeAuthorization()
        {
            TempData.Remove(
                PhoneChangeAuthorizedUntilKey);
        }

        // =====================================================
        // PHONE NORMALIZATION
        // =====================================================
        private static string NormalizeLebanesePhone(
            string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(
                    phoneNumber))
            {
                return string.Empty;
            }

            var value =
                Regex.Replace(
                    phoneNumber.Trim(),
                    @"[\s()-]",
                    string.Empty);

            if (value.StartsWith(
                    "00961",
                    StringComparison.Ordinal))
            {
                value =
                    "+" + value.Substring(2);
            }
            else if (value.StartsWith(
                         "961",
                         StringComparison.Ordinal))
            {
                value =
                    "+" + value;
            }
            else if (value.StartsWith(
                         "0",
                         StringComparison.Ordinal))
            {
                value =
                    "+961" + value.Substring(1);
            }
            else if (!value.StartsWith(
                         "+",
                         StringComparison.Ordinal))
            {
                value =
                    "+961" + value;
            }

            return Regex.IsMatch(
                value,
                @"^\+961[0-9]{7,8}$")
                    ? value
                    : string.Empty;
        }

        private static bool StatusEquals(
            string? currentStatus,
            string expectedStatus)
        {
            return string.Equals(
                currentStatus?.Trim(),
                expectedStatus,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetIdentityErrors(
            IdentityResult result)
        {
            return string.Join(
                " | ",
                result.Errors.Select(
                    error =>
                        error.Description));
        }

        // =====================================================
        // LOCAL TIME
        // =====================================================
        public string FormatLocalTime(
            DateTime? value)
        {
            if (!value.HasValue)
            {
                return "Not available";
            }

            var utcDate =
                DateTime.SpecifyKind(
                    value.Value,
                    DateTimeKind.Utc);

            return utcDate
                .ToLocalTime()
                .ToString(
                    "dd MMM yyyy - HH:mm");
        }
    }

    // =========================================================
    // NORMAL PROFILE INPUT
    // =========================================================
    public class DeliveryProfileInput
    {
        [Required(
            ErrorMessage =
                "Full name is required.")]
        public string FullName { get; set; }
            = string.Empty;

        /*
         * Read-only until the current phone OTP is verified.
         * It becomes editable for ten minutes after verification.
         */
        public string PhoneNumber { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Area is required.")]
        public string Area { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Vehicle type is required.")]
        public string VehicleType { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Vehicle number is required.")]
        public string VehicleNumber { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Driving license number is required.")]
        public string DrivingLicenseNumber { get; set; }
            = string.Empty;
    }

    // =========================================================
    // PROFILE VIEW MODEL
    // =========================================================
    public class DeliveryProfileViewModel
    {
        public int DeliveryPersonID { get; set; }

        public int UserID { get; set; }

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

        public string Status { get; set; }
            = string.Empty;

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

    // =========================================================
    // ASSIGNMENT VIEW MODEL
    // =========================================================
    public class DeliveryProfileAssignmentViewModel
    {
        public int AssignmentID { get; set; }

        public int OrderID { get; set; }

        public string OrderNumber { get; set; }
            = string.Empty;

        public string OrderStatus { get; set; }
            = string.Empty;

        public string AssignmentStatus { get; set; }
            = string.Empty;

        public string CustomerAddress { get; set; }
            = string.Empty;

        public DateTime AssignedAt { get; set; }

        public DateTime? PickupTime { get; set; }

        public DateTime? DeliveryTime { get; set; }

        public decimal TotalAmount { get; set; }

        public string PaymentMethod { get; set; }
            = string.Empty;

        public string PaymentStatus { get; set; }
            = string.Empty;
    }
}