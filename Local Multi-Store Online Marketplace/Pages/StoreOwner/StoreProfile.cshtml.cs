using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner
{
    [Authorize(Roles = "StoreOwner")]
    public class StoreProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreService _currentStoreService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<StoreProfileModel> _logger;

        // BUGFIX: previously only checked the file extension — no Content-Type
        // cross-check, no magic-byte signature check, and no size limit at all.
        // Same category of gap already found and fixed on Products/Create and
        // Products/Edit; applied the same way here.
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedImageMimeTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxLogoSizeBytes = 5 * 1024 * 1024; // 5 MB

        public StoreProfileModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment,
            ILogger<StoreProfileModel> logger)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        [BindProperty]
        public StoreProfileInputModel StoreVM { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                if (!await _currentStoreService.IsStoreOwnerAsync())
                {
                    return RedirectToPage("/Account/AccessDenied", new { area = "Identity" });
                }

                var store = await _currentStoreService.GetCurrentStoreAsync();

                if (store == null)
                {
                    TempData["ErrorMessage"] = "Store not found. Please make sure your store is approved.";
                    return RedirectToPage("/StoreOwner/Dashboard");
                }

                ViewData["StoreName"] = store.StoreName;
                ViewData["StoreId"] = store.StoreID;

                StoreVM = new StoreProfileInputModel
                {
                    StoreID = store.StoreID,
                    StoreName = store.StoreName,
                    Description = store.Description,
                    LogoURL = store.LogoURL,
                    PhoneNumber = store.PhoneNumber,
                    Email = store.Email,
                    AddressLine1 = store.AddressLine1,
                    AddressLine2 = store.AddressLine2,
                    City = store.City,
                    Area = store.Area,
                    Latitude = store.Latitude,
                    Longitude = store.Longitude,
                    BusinessLicenseNumber = store.BusinessLicenseNumber,
                    BusinessLicenseURL = store.BusinessLicenseURL,
                    Status = store.Status,
                    CODSupported = store.CODSupported,
                    CODMaxLimit = store.CODMaxLimit
                };

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Store Profile page.");
                TempData["ErrorMessage"] = "Something went wrong while loading your store profile. Please try again.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await _currentStoreService.IsStoreOwnerAsync())
            {
                return RedirectToPage("/Account/AccessDenied", new { area = "Identity" });
            }

            var store = await _currentStoreService.GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] = "Store not found.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            try
            {
                StoreVM.StoreName = StoreVM.StoreName?.Trim() ?? string.Empty;
                StoreVM.Description = StoreVM.Description?.Trim() ?? string.Empty;
                StoreVM.PhoneNumber = StoreVM.PhoneNumber?.Trim() ?? string.Empty;
                StoreVM.Email = StoreVM.Email?.Trim() ?? string.Empty;
                StoreVM.AddressLine1 = StoreVM.AddressLine1?.Trim() ?? string.Empty;
                StoreVM.AddressLine2 = StoreVM.AddressLine2?.Trim();
                StoreVM.City = StoreVM.City?.Trim() ?? string.Empty;
                StoreVM.Area = StoreVM.Area?.Trim() ?? string.Empty;
                StoreVM.BusinessLicenseNumber = StoreVM.BusinessLicenseNumber?.Trim();

                if (string.IsNullOrWhiteSpace(StoreVM.StoreName))
                {
                    ModelState.AddModelError("StoreVM.StoreName", "Store name is required.");
                }

                if (string.IsNullOrWhiteSpace(StoreVM.Email))
                {
                    ModelState.AddModelError("StoreVM.Email", "Email is required.");
                }
                else if (!IsValidEmail(StoreVM.Email))
                {
                    // BUGFIX: previously only checked that Email was non-empty, not
                    // that it was actually a valid email address. store.Email is
                    // used elsewhere to create the Stripe Customer for this store
                    // (see StoreOwnerPaymentModel.GetOrCreateCustomerAsync) — an
                    // invalid address saved here could silently break that later.
                    ModelState.AddModelError("StoreVM.Email", "Please enter a valid email address.");
                }

                if (string.IsNullOrWhiteSpace(StoreVM.PhoneNumber))
                {
                    ModelState.AddModelError("StoreVM.PhoneNumber", "Phone number is required.");
                }

                if (string.IsNullOrWhiteSpace(StoreVM.AddressLine1))
                {
                    ModelState.AddModelError("StoreVM.AddressLine1", "Address is required.");
                }

                if (string.IsNullOrWhiteSpace(StoreVM.City))
                {
                    ModelState.AddModelError("StoreVM.City", "City is required.");
                }

                if (string.IsNullOrWhiteSpace(StoreVM.Area))
                {
                    ModelState.AddModelError("StoreVM.Area", "Area is required.");
                }

                // BUGFIX: Latitude/Longitude had no validation at all — a store
                // owner could save geographically impossible values (e.g.
                // Latitude = 999), silently breaking delivery-distance
                // calculations elsewhere that assume valid coordinates.
                if (StoreVM.Latitude < -90 || StoreVM.Latitude > 90)
                {
                    ModelState.AddModelError("StoreVM.Latitude", "Latitude must be between -90 and 90.");
                }

                if (StoreVM.Longitude < -180 || StoreVM.Longitude > 180)
                {
                    ModelState.AddModelError("StoreVM.Longitude", "Longitude must be between -180 and 180.");
                }

                if (StoreVM.CODMaxLimit < 0)
                {
                    ModelState.AddModelError("StoreVM.CODMaxLimit", "COD max limit cannot be negative.");
                }

                // BUGFIX: logo validation was extension-only — no size limit, no
                // Content-Type cross-check, no magic-byte signature check. A file
                // renamed to end in .jpg (regardless of its real contents) would
                // previously pass straight through.
                if (StoreVM.LogoFile != null && StoreVM.LogoFile.Length > 0)
                {
                    var logoError = await ValidateLogoFileAsync(StoreVM.LogoFile);
                    if (logoError != null)
                    {
                        ModelState.AddModelError("StoreVM.LogoFile", logoError);
                    }
                }

                if (!ModelState.IsValid)
                {
                    StoreVM.LogoURL = store.LogoURL;
                    StoreVM.Status = store.Status;
                    StoreVM.BusinessLicenseURL = store.BusinessLicenseURL;
                    ViewData["StoreName"] = store.StoreName;
                    ViewData["StoreId"] = store.StoreID;
                    return Page();
                }

                if (StoreVM.LogoFile != null && StoreVM.LogoFile.Length > 0)
                {
                    store.LogoURL = await SaveStoreLogoAsync(store.StoreID, StoreVM.LogoFile);
                }

                store.StoreName = StoreVM.StoreName;
                store.Description = StoreVM.Description;
                store.PhoneNumber = StoreVM.PhoneNumber;
                store.Email = StoreVM.Email;
                store.AddressLine1 = StoreVM.AddressLine1;
                store.AddressLine2 = StoreVM.AddressLine2;
                store.City = StoreVM.City;
                store.Area = StoreVM.Area;
                store.Latitude = StoreVM.Latitude;
                store.Longitude = StoreVM.Longitude;
                store.BusinessLicenseNumber = StoreVM.BusinessLicenseNumber;
                store.CODSupported = StoreVM.CODSupported;
                store.CODMaxLimit = StoreVM.CODMaxLimit;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Store profile updated successfully.";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Store Profile for store {StoreId}.", store.StoreID);
                ModelState.AddModelError("", "Something went wrong while saving your changes. Please try again.");
                StoreVM.LogoURL = store.LogoURL;
                StoreVM.Status = store.Status;
                StoreVM.BusinessLicenseURL = store.BusinessLicenseURL;
                ViewData["StoreName"] = store.StoreName;
                ViewData["StoreId"] = store.StoreID;
                return Page();
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates the uploaded store logo: size, extension/MIME type, and the
        /// actual file signature (magic bytes) — extension and Content-Type are
        /// both client-supplied and easily spoofed, so the header bytes are
        /// checked too before anything is trusted or saved to disk.
        /// </summary>
        private static async Task<string?> ValidateLogoFileAsync(IFormFile logoFile)
        {
            if (logoFile.Length > MaxLogoSizeBytes)
            {
                return "Logo image is too large. Maximum size is 5 MB.";
            }

            var extension = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
            var contentType = logoFile.ContentType?.ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension) || !AllowedImageMimeTypes.Contains(contentType))
            {
                return "Only JPG, JPEG, PNG, or WEBP images are allowed.";
            }

            if (!await HasValidImageSignatureAsync(logoFile))
            {
                return "That file doesn't look like a valid image. Please try a different file.";
            }

            return null;
        }

        private static async Task<bool> HasValidImageSignatureAsync(IFormFile file)
        {
            var buffer = new byte[12];
            int bytesRead;

            using (var stream = file.OpenReadStream())
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            }

            if (bytesRead < 4) return false;

            bool StartsWith(byte[] signature, int offset = 0)
            {
                if (buffer.Length < offset + signature.Length) return false;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (buffer[offset + i] != signature[i]) return false;
                }
                return true;
            }

            if (StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return true;                 // JPEG
            if (StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return true;            // PNG
            if (bytesRead >= 12 &&
                StartsWith(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0) &&                    // "RIFF"
                StartsWith(new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8))                       // "WEBP"
                return true;

            return false;
        }

        private async Task<string> SaveStoreLogoAsync(int storeId, IFormFile logoFile)
        {
            string uploadFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "uploads",
                "stores",
                storeId.ToString());

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            string extension = Path.GetExtension(logoFile.FileName);
            string uniqueFileName = $"logo_{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(uploadFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await logoFile.CopyToAsync(stream);
            }

            return $"/uploads/stores/{storeId}/{uniqueFileName}";
        }

        public class StoreProfileInputModel
        {
            public int StoreID { get; set; }

            public string StoreName { get; set; } = string.Empty;

            public string Description { get; set; } = string.Empty;

            public string? LogoURL { get; set; }

            public IFormFile? LogoFile { get; set; }

            public string PhoneNumber { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string AddressLine1 { get; set; } = string.Empty;

            public string? AddressLine2 { get; set; }

            public string City { get; set; } = string.Empty;

            public string Area { get; set; } = string.Empty;

            public decimal Latitude { get; set; }

            public decimal Longitude { get; set; }

            public string? BusinessLicenseNumber { get; set; }

            public string? BusinessLicenseURL { get; set; }

            public string Status { get; set; } = string.Empty;

            public bool CODSupported { get; set; }

            public decimal CODMaxLimit { get; set; }
        }
    }
}