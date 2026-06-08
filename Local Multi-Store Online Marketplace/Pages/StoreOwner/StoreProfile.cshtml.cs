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

        public StoreProfileModel(
            ApplicationDbContext context,
            ICurrentStoreService currentStoreService,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _currentStoreService = currentStoreService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public StoreProfileInputModel StoreVM { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
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

            if (StoreVM.CODMaxLimit < 0)
            {
                ModelState.AddModelError("StoreVM.CODMaxLimit", "COD max limit cannot be negative.");
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
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(StoreVM.LogoFile.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("StoreVM.LogoFile", "Only JPG, JPEG, PNG, or WEBP images are allowed.");
                    StoreVM.LogoURL = store.LogoURL;
                    StoreVM.Status = store.Status;
                    ViewData["StoreName"] = store.StoreName;
                    ViewData["StoreId"] = store.StoreID;
                    return Page();

                }

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