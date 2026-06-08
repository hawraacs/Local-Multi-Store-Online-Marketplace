using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminSettingsModel : PageModel
    {
        [BindProperty]
        public string PlatformName { get; set; } = "Multi-Store Marketplace";
        [BindProperty]
        public decimal CommissionRate { get; set; } = 10.0m;
        [BindProperty]
        public decimal BaseDeliveryFee { get; set; } = 2.0m;
        [BindProperty]
        public decimal FreeDeliveryThreshold { get; set; } = 50.0m;
        [BindProperty]
        public bool MaintenanceMode { get; set; }

        public void OnGet()
        {
            // Load settings from database or config
        }

        public IActionResult OnPost()
        {
            // Save settings to database or app settings
            TempData["Success"] = "Settings saved successfully.";
            return RedirectToPage();
        }
    }
}