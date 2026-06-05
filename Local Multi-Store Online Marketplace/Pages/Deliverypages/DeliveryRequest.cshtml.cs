using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    public class DeliveryRequestModel : PageModel
    {
        private readonly DeliveryManager _deliveryManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<DeliveryRequestModel> _logger;

        public DeliveryRequestModel(
            DeliveryManager deliveryManager,
            UserManager<User> userManager,
            ILogger<DeliveryRequestModel> logger)
        {
            _deliveryManager = deliveryManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Delivery request POST started.");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid.");
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("User not logged in.");
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            Delivery.UserID = user.Id;

            try
            {
                var id = await _deliveryManager.RegisterDeliveryPersonAsync(Delivery);

                _logger.LogInformation("Delivery created with ID: {Id}", id);

                TempData["Success"] = "Delivery staff request submitted successfully. Waiting for admin approval.";

                return RedirectToPage("/CustomerProfile");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while submitting delivery request.");
                ModelState.AddModelError(string.Empty, "An error occurred while submitting your request.");
                return Page();
            }
        }
    }
}