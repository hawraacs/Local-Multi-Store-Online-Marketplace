using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerProfileModel : PageModel
    {
        // =========================
        // CUSTOMER PROFILE (FLAT)
        // =========================
        [BindProperty]
        public string CustomerFullName { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerEmail { get; set; } = string.Empty;

        [BindProperty]
        public string CustomerPhone { get; set; } = string.Empty;

        // =========================
        // STORE REQUEST
        // =========================
        [BindProperty]
        public StoreDTO Store { get; set; } = new StoreDTO();

        // =========================
        // DELIVERY REQUEST
        // =========================
        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new DeliveryPersonDTO();

        // =========================
        // GET
        // =========================
        public void OnGet()
        {
            // later: load logged-in user from DB/session
        }

        // =========================
        // UPDATE PROFILE
        // =========================
        public IActionResult OnPost()
        {
            // update customer profile logic later (service layer)
            return Page();
        }

        // =========================
        // STORE REQUEST
        // =========================
        public IActionResult OnPostStoreRequest()
        {
            Store.Status = "Pending";
            // send store request to admin later
            return Page();
        }

        // =========================
        // DELIVERY REQUEST
        // =========================
        public IActionResult OnPostDeliveryRequest()
        {
            Delivery.Status = "Pending";
            // send delivery request to admin later
            return Page();
        }
    }
}