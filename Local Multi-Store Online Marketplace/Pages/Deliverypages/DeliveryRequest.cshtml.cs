using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    public class DeliveryRequestModel : PageModel
    {
        private readonly DeliveryManager _deliveryManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DeliveryRequestModel> _logger;

        public DeliveryRequestModel(
            DeliveryManager deliveryManager,
            UserManager<User> userManager,
            ApplicationDbContext dbContext,
            ILogger<DeliveryRequestModel> logger)
        {
            _deliveryManager = deliveryManager;
            _userManager = userManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty]
        public DeliveryPersonDTO Delivery { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Delivery request POST started");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid");
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("User not logged in");
                return RedirectToPage("/Identity/Account/Login");
            }

            Delivery.UserID = user.Id;

            var id = await _deliveryManager.RegisterDeliveryPersonAsync(Delivery);

            _logger.LogInformation("Delivery created with ID: {Id}", id);

            return RedirectToPage("/Customer1");
        }
    }
}