using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class StoreRequestModel : PageModel
    {
        private static readonly Dictionary<string, string>
            CityAreaMap =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Beirut"] = "Beirut",
                    ["Tripoli"] = "North Lebanon",
                    ["Saida"] = "South Lebanon",
                    ["Tyre"] = "South Lebanon",
                    ["Zahle"] = "Bekaa",
                    ["Jounieh"] = "Mount Lebanon",
                    ["Byblos"] = "Mount Lebanon",
                    ["Nabatieh"] = "Nabatieh",
                    ["Aley"] = "Mount Lebanon",
                    ["Baabda"] = "Mount Lebanon"
                };

        private static readonly HashSet<string>
            AllowedLicenseExtensions =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".pdf"
                };

        private static readonly HashSet<string>
            AllowedLicenseContentTypes =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    "image/jpeg",
                    "image/png",
                    "application/pdf"
                };

        private const long MaximumLicenseFileSize =
            5 * 1024 * 1024;

        private const string StoreRequestInputTempDataKey =
            "StoreRequestInput";

        private readonly StoreManager _storeManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<StoreRequestModel> _logger;
        private readonly IWebHostEnvironment _environment;

        public StoreRequestModel(
            StoreManager storeManager,
            UserManager<User> userManager,
            ILogger<StoreRequestModel> logger,
            IWebHostEnvironment environment)
        {
            _storeManager = storeManager;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
        }

        [BindProperty]
        public StoreRequestInputModel Input { get; set; }
            = new();

        [BindProperty]
        public IFormFile? BusinessLicenseFile { get; set; }

        public string? ExistingBusinessLicenseURL { get; set; }

        public bool IsResubmission { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var existingStore =
                await _storeManager
                    .GetByRequestedByUserIdAsync(
                        user.Id);

            if (existingStore != null)
            {
                var status =
                    existingStore.Status?.Trim();

                if (StatusEquals(
                        status,
                        "Pending"))
                {
                    TempData["Error"] =
                        "You already have a pending store request.";

                    return RedirectToPage(
                        "/CustomerProfile");
                }

                if (StatusEquals(
                        status,
                        "Approved"))
                {
                    TempData["Error"] =
                        "You already own an approved store.";

                    return RedirectToPage(
                        "/CustomerProfile");
                }

                if (StatusEquals(
                        status,
                        "Inactive") ||
                    StatusEquals(
                        status,
                        "Suspended"))
                {
                    TempData["Error"] =
                        "A store account already exists but is currently inactive.";

                    return RedirectToPage(
                        "/CustomerProfile");
                }
            }

            IsResubmission =
                existingStore != null &&
                StatusEquals(
                    existingStore.Status,
                    "Rejected");

            ExistingBusinessLicenseURL =
                existingStore?.BusinessLicenseURL;

            /*
             * After a failed POST, restore the entered values on this GET.
             * This implements POST-Redirect-GET, so browser Back/Refresh
             * no longer asks to resubmit the form.
             */
            if (TryRestoreInputFromTempData())
            {
                return Page();
            }

            if (IsResubmission &&
                existingStore != null)
            {
                Input =
                    new StoreRequestInputModel
                    {
                        StoreName =
                            existingStore.StoreName,

                        Email =
                            existingStore.Email,

                        PhoneNumber =
                            existingStore.PhoneNumber,

                        BusinessLicenseNumber =
                            existingStore.BusinessLicenseNumber
                            ?? string.Empty,

                        Description =
                            existingStore.Description,

                        AddressLine1 =
                            existingStore.AddressLine1,

                        City =
                            existingStore.City,

                        Area =
                            existingStore.Area,

                        Latitude =
                            existingStore.Latitude,

                        Longitude =
                            existingStore.Longitude
                    };

                return Page();
            }

            Input.Email =
                user.Email
                ?? string.Empty;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var existingStore =
                await _storeManager
                    .GetByRequestedByUserIdAsync(
                        user.Id);

            IsResubmission =
                existingStore != null &&
                StatusEquals(
                    existingStore.Status,
                    "Rejected");

            ExistingBusinessLicenseURL =
                existingStore?.BusinessLicenseURL;

            Input.StoreName =
                Input.StoreName?.Trim()
                ?? string.Empty;

            Input.Email =
                Input.Email?.Trim()
                    .ToLowerInvariant()
                ?? string.Empty;

            Input.PhoneNumber =
                NormalizeLebanesePhone(
                    Input.PhoneNumber);

            Input.BusinessLicenseNumber =
                Input.BusinessLicenseNumber?.Trim()
                ?? string.Empty;

            Input.Description =
                Input.Description?.Trim()
                ?? string.Empty;

            Input.AddressLine1 =
                Input.AddressLine1?.Trim()
                ?? string.Empty;

            Input.City =
                Input.City?.Trim()
                ?? string.Empty;

            Input.Area =
                Input.Area?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(
                    Input.PhoneNumber))
            {
                ModelState.AddModelError(
                    "Input.PhoneNumber",
                    "Enter a valid Lebanese phone number, for example +96178123456.");
            }

            if (!IsValidCityArea(
                    Input.City,
                    Input.Area))
            {
                ModelState.AddModelError(
                    "Input.Area",
                    "The selected area does not match the selected city.");
            }

            if (!Input.Latitude.HasValue ||
                !Input.Longitude.HasValue)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Select the store location on the map.");
            }
            else if (!IsInsideLebanon(
                         Input.Latitude.Value,
                         Input.Longitude.Value))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The selected map location must be inside Lebanon.");
            }

            if (!Input.ConfirmInformation)
            {
                ModelState.AddModelError(
                    "Input.ConfirmInformation",
                    "Confirm that the submitted information is accurate.");
            }

            var hasExistingLicense =
                !string.IsNullOrWhiteSpace(
                    ExistingBusinessLicenseURL);

            if ((BusinessLicenseFile == null ||
                 BusinessLicenseFile.Length == 0) &&
                !hasExistingLicense)
            {
                ModelState.AddModelError(
                    nameof(BusinessLicenseFile),
                    "Upload the business license document.");
            }

            ValidateBusinessLicenseFile();

            if (!ModelState.IsValid)
            {
                return RedirectWithCurrentErrors(
                    "Please correct the highlighted information.");
            }

            string? savedPhysicalFilePath =
                null;

            try
            {
                var businessLicenseUrl =
                    ExistingBusinessLicenseURL;

                if (BusinessLicenseFile != null &&
                    BusinessLicenseFile.Length > 0)
                {
                    var savedFile =
                        await SaveBusinessLicenseFileAsync(
                            BusinessLicenseFile);

                    businessLicenseUrl =
                        savedFile.publicUrl;

                    savedPhysicalFilePath =
                        savedFile.physicalPath;
                }

                var storeDto =
                    new StoreDTO
                    {
                        OwnerUserID =
                            user.Id,

                        RequestedByUserID =
                            user.Id,

                        StoreName =
                            Input.StoreName,

                        Email =
                            Input.Email,

                        PhoneNumber =
                            Input.PhoneNumber,

                        BusinessLicenseNumber =
                            Input.BusinessLicenseNumber,

                        BusinessLicenseURL =
                            businessLicenseUrl,

                        Description =
                            Input.Description,

                        AddressLine1 =
                            Input.AddressLine1,

                        City =
                            Input.City,

                        Area =
                            Input.Area,

                        Latitude =
                            Input.Latitude!.Value,

                        Longitude =
                            Input.Longitude!.Value,

                        Status =
                            "Pending"
                    };

                var registrationResult =
                    await _storeManager
                        .TryRegisterStoreAsync(
                            storeDto);

                if (!registrationResult.Succeeded)
                {
                    DeleteUploadedFileIfNeeded(
                        savedPhysicalFilePath);

                    PersistInputForRedirect();

                    TempData["Error"] =
                        BuildRedirectErrorMessage(
                            registrationResult.Message,
                            savedPhysicalFilePath != null);

                    return RedirectToPage(
                        "/StoreRequest");
                }

                TempData["Success"] =
                    IsResubmission
                        ? "Your updated store request was resubmitted and is waiting for admin approval."
                        : "Your store request was submitted and is waiting for admin approval.";

                return RedirectToPage(
                    "/CustomerProfile");
            }
            catch (Exception ex)
            {
                DeleteUploadedFileIfNeeded(
                    savedPhysicalFilePath);

                _logger.LogError(
                    ex,
                    "Failed to submit store request for customer {CustomerId}.",
                    user.Id);

                PersistInputForRedirect();

                TempData["Error"] =
                    BuildRedirectErrorMessage(
                        "The store request could not be submitted. Please try again.",
                        savedPhysicalFilePath != null);

                return RedirectToPage(
                    "/StoreRequest");
            }
        }

        // =====================================================
        // POST-REDIRECT-GET HELPERS
        // =====================================================
        private IActionResult RedirectWithCurrentErrors(
            string fallbackMessage)
        {
            var messages =
                ModelState.Values
                    .SelectMany(
                        value =>
                            value.Errors)
                    .Select(
                        error =>
                            !string.IsNullOrWhiteSpace(
                                error.ErrorMessage)
                                ? error.ErrorMessage
                                : fallbackMessage)
                    .Where(
                        message =>
                            !string.IsNullOrWhiteSpace(
                                message))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (BusinessLicenseFile != null &&
                BusinessLicenseFile.Length > 0)
            {
                messages.Add(
                    "Please select the business license document again.");
            }

            PersistInputForRedirect();

            TempData["Error"] =
                messages.Count > 0
                    ? string.Join(
                        " | ",
                        messages)
                    : fallbackMessage;

            return RedirectToPage(
                "/StoreRequest");
        }

        private void PersistInputForRedirect()
        {
            TempData[StoreRequestInputTempDataKey] =
                JsonSerializer.Serialize(
                    Input);
        }

        private bool TryRestoreInputFromTempData()
        {
            if (TempData[
                    StoreRequestInputTempDataKey]
                is not string serializedInput ||
                string.IsNullOrWhiteSpace(
                    serializedInput))
            {
                return false;
            }

            try
            {
                var restoredInput =
                    JsonSerializer.Deserialize<
                        StoreRequestInputModel>(
                        serializedInput);

                if (restoredInput == null)
                {
                    return false;
                }

                Input =
                    restoredInput;

                return true;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not restore Store Request form values from TempData.");

                return false;
            }
        }

        private static string BuildRedirectErrorMessage(
            string message,
            bool uploadedFileWasRemoved)
        {
            if (!uploadedFileWasRemoved)
            {
                return message;
            }

            return
                message +
                " Please select the business license document again.";
        }

        private void ValidateBusinessLicenseFile()
        {
            if (BusinessLicenseFile == null ||
                BusinessLicenseFile.Length == 0)
            {
                return;
            }

            if (BusinessLicenseFile.Length >
                MaximumLicenseFileSize)
            {
                ModelState.AddModelError(
                    nameof(BusinessLicenseFile),
                    "The business license file must be 5 MB or smaller.");
            }

            var extension =
                Path.GetExtension(
                    BusinessLicenseFile.FileName);

            if (!AllowedLicenseExtensions.Contains(
                    extension))
            {
                ModelState.AddModelError(
                    nameof(BusinessLicenseFile),
                    "Only JPG, JPEG, PNG, and PDF files are allowed.");
            }

            if (!AllowedLicenseContentTypes.Contains(
                    BusinessLicenseFile.ContentType))
            {
                ModelState.AddModelError(
                    nameof(BusinessLicenseFile),
                    "The selected business license file type is not allowed.");
            }
        }

        private async Task<(string publicUrl, string physicalPath)>
            SaveBusinessLicenseFileAsync(
                IFormFile file)
        {
            var extension =
                Path.GetExtension(
                    file.FileName)
                    .ToLowerInvariant();

            var uploadsFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "store-license-proofs");

            Directory.CreateDirectory(
                uploadsFolder);

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var physicalPath =
                Path.Combine(
                    uploadsFolder,
                    fileName);

            await using var stream =
                new FileStream(
                    physicalPath,
                    FileMode.CreateNew);

            await file.CopyToAsync(
                stream);

            return (
                $"/uploads/store-license-proofs/{fileName}",
                physicalPath);
        }

        private static void DeleteUploadedFileIfNeeded(
            string? physicalPath)
        {
            if (string.IsNullOrWhiteSpace(
                    physicalPath))
            {
                return;
            }

            try
            {
                if (System.IO.File.Exists(
                        physicalPath))
                {
                    System.IO.File.Delete(
                        physicalPath);
                }
            }
            catch
            {
                // Do not hide the original request error.
            }
        }

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

        private static bool IsValidCityArea(
            string city,
            string area)
        {
            return CityAreaMap.TryGetValue(
                       city,
                       out var expectedArea)
                   &&
                   string.Equals(
                       expectedArea,
                       area,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideLebanon(
            decimal latitude,
            decimal longitude)
        {
            return latitude >= 33.0m &&
                   latitude <= 34.8m &&
                   longitude >= 35.0m &&
                   longitude <= 36.8m;
        }

        private static bool StatusEquals(
            string? current,
            string expected)
        {
            return string.Equals(
                current?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        public class StoreRequestInputModel
        {
            [Required(
                ErrorMessage =
                    "Store name is required.")]
            [StringLength(
                100,
                MinimumLength = 2,
                ErrorMessage =
                    "Store name must contain between 2 and 100 characters.")]
            public string StoreName { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Business contact email is required.")]
            [EmailAddress(
                ErrorMessage =
                    "Enter a valid business contact email.")]
            [StringLength(
                150,
                ErrorMessage =
                    "Business contact email is too long.")]
            public string Email { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Phone number is required.")]
            [StringLength(
                20,
                ErrorMessage =
                    "Phone number is too long.")]
            public string PhoneNumber { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Business license number is required.")]
            [StringLength(
                50,
                MinimumLength = 3,
                ErrorMessage =
                    "Business license number must contain between 3 and 50 characters.")]
            public string BusinessLicenseNumber { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Store description is required.")]
            [StringLength(
                1000,
                MinimumLength = 20,
                ErrorMessage =
                    "Store description must contain between 20 and 1000 characters.")]
            public string Description { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Store address is required.")]
            [StringLength(
                250,
                MinimumLength = 5,
                ErrorMessage =
                    "Store address must contain between 5 and 250 characters.")]
            public string AddressLine1 { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "City is required.")]
            public string City { get; set; }
                = string.Empty;

            [Required(
                ErrorMessage =
                    "Area is required.")]
            public string Area { get; set; }
                = string.Empty;

            public decimal? Latitude { get; set; }

            public decimal? Longitude { get; set; }

            public bool ConfirmInformation { get; set; }
        }
    }
}