using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<AdminDeliveryModel> _logger;

        public AdminDeliveryModel(
            DeliveryManager deliveryManager,
            ILogger<AdminDeliveryModel> logger,
            ApplicationDbContext dbContext)
        {
            _deliveryManager = deliveryManager;
            _logger = logger;
            _dbContext = dbContext;
        }

        public List<DeliveryPersonDTO> Deliveries { get; set; } = new();

        public async Task OnGetAsync()
        {
            _logger.LogInformation("ADMIN DELIVERY PAGE LOADED");

            var list = await _deliveryManager.GetAllAsync();

            _logger.LogInformation(
                "ADMIN DB: {DbName}",
                _dbContext.Database.GetDbConnection().Database);

            _logger.LogInformation("DELIVERY COUNT = {Count}", list.Count);

            // IMPORTANT:
            // Show ALL delivery requests, not only Pending.
            // This keeps Approved / Rejected rows visible in the table.
            Deliveries = list
                .OrderByDescending(d => d.DeliveryPersonID)
                .ToList();

            _logger.LogInformation("DISPLAY COUNT = {Count}", Deliveries.Count);
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            await _deliveryManager.ApproveDeliveryPersonAsync(id);

            TempData["Success"] = "Delivery request approved successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            await _deliveryManager.RejectDeliveryPersonAsync(id);

            TempData["Success"] = "Delivery request rejected successfully.";

            return RedirectToPage();
        }
    }
}