using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.ComponentModel.DataAnnotations;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Settings
{
    [Authorize(Roles = "StoreOwner")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public StoreSettingsInputModel StoreSettings { get; set; } = new();

        public string StoreName { get; set; } = string.Empty;

        public string StoreCode { get; set; } = string.Empty;

        public string CurrentStatus { get; set; } = string.Empty;

        public decimal Rating { get; set; }

        public int TotalRatings { get; set; }

        public decimal CommissionRate { get; set; }

        public string? BusinessLicenseNumber { get; set; }

        public string? BusinessLicenseURL { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public string StoreLocationSummary { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var store = await GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["Error"] = "No store is connected to your account.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            LoadStoreData(store);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var store = await GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["Error"] = "No store is connected to your account.";
                return RedirectToPage("/StoreOwner/Dashboard");
            }

            LoadReadonlyData(store);

            ValidateStoreSettings();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            store.HasFixedDeliveryFee = StoreSettings.HasFixedDeliveryFee;

            if (StoreSettings.HasFixedDeliveryFee)
            {
                store.FixedDeliveryFee = StoreSettings.FixedDeliveryFee;
            }
            else
            {
                store.FixedDeliveryFee = null;
            }

            store.CODSupported = StoreSettings.CODSupported;
            store.CODMaxLimit = StoreSettings.CODMaxLimit;

            if (StoreSettings.AvailabilityStatus == "Open")
            {
                if (store.Status == "Inactive")
                {
                    store.Status = store.ApprovedAt.HasValue ? "Approved" : "Active";
                }
            }
            else if (StoreSettings.AvailabilityStatus == "Inactive")
            {
                if (store.Status == "Approved" || store.Status == "Active" || store.Status == "Inactive")
                {
                    store.Status = "Inactive";
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Operational settings updated successfully.";

            return RedirectToPage();
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            return await _context.Stores
                .FirstOrDefaultAsync(s => s.OwnerUserID == user.Id);
        }

        private void LoadStoreData(Store store)
        {
            LoadReadonlyData(store);

            StoreSettings = new StoreSettingsInputModel
            {
                HasFixedDeliveryFee = store.HasFixedDeliveryFee,
                FixedDeliveryFee = store.FixedDeliveryFee,
                CODSupported = store.CODSupported,
                CODMaxLimit = store.CODMaxLimit,
                AvailabilityStatus = store.Status == "Inactive" ? "Inactive" : "Open"
            };
        }

        private void LoadReadonlyData(Store store)
        {
            StoreName = store.StoreName;
            StoreCode = store.StoreCode;
            CurrentStatus = store.Status;
            Rating = store.Rating;
            TotalRatings = store.TotalRatings;
            CommissionRate = store.CommissionRate;
            BusinessLicenseNumber = store.BusinessLicenseNumber;
            BusinessLicenseURL = store.BusinessLicenseURL;
            CreatedAt = store.CreatedAt;
            ApprovedAt = store.ApprovedAt;

            StoreLocationSummary = $"{store.AddressLine1}, {store.Area}, {store.City}";
        }

        private void ValidateStoreSettings()
        {
            if (StoreSettings.HasFixedDeliveryFee)
            {
                if (StoreSettings.FixedDeliveryFee == null)
                {
                    ModelState.AddModelError(
                        "StoreSettings.FixedDeliveryFee",
                        "Fixed delivery fee is required when fixed delivery is enabled.");
                }
                else if (StoreSettings.FixedDeliveryFee < 0)
                {
                    ModelState.AddModelError(
                        "StoreSettings.FixedDeliveryFee",
                        "Fixed delivery fee cannot be negative.");
                }
            }

            if (StoreSettings.CODMaxLimit < 0)
            {
                ModelState.AddModelError(
                    "StoreSettings.CODMaxLimit",
                    "COD max limit cannot be negative.");
            }

            if (StoreSettings.AvailabilityStatus != "Open" &&
                StoreSettings.AvailabilityStatus != "Inactive")
            {
                ModelState.AddModelError(
                    "StoreSettings.AvailabilityStatus",
                    "Invalid availability status.");
            }
        }
    }

    public class StoreSettingsInputModel
    {
        public bool HasFixedDeliveryFee { get; set; }

        [Range(0, 999999)]
        public decimal? FixedDeliveryFee { get; set; }

        public bool CODSupported { get; set; }

        [Range(0, 999999)]
        public decimal CODMaxLimit { get; set; }

        [Required]
        public string AvailabilityStatus { get; set; } = "Open";
    }
}