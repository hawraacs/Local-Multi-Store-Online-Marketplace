using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.Data.Entity;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]

    public class AdminDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<AdminDeliveryModel> _logger;


        public AdminDeliveryModel(DeliveryManager deliveryManager, ILogger<AdminDeliveryModel> logger, ApplicationDbContext dbContext)
        {
            _deliveryManager = deliveryManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        public List<DeliveryPersonDTO> Deliveries { get; set; } = new();

        // =====================
        public async Task OnGetAsync()
        {
            _logger.LogInformation("ADMIN DELIVERY PAGE LOADED");

            var list = await _deliveryManager.GetAllAsync();

            _logger.LogInformation("COUNT = {Count}", list.Count);

            _logger.LogInformation("ADMIN DB: {DbName}", _dbContext.Database.GetDbConnection().Database);

            _logger.LogInformation("RAW DB COUNT: {Count}", list.Count);

            foreach (var item in list)
            {
                _logger.LogInformation("ID={Id}, Status={Status}",
                    item.DeliveryPersonID, item.Status);
            }

            Deliveries = list.Where(x => x.Status != null && x.Status.ToLower() == "pending")
                .Select(x => new DeliveryPersonDTO
                {
                    DeliveryPersonID = x.DeliveryPersonID,
                    FullName = x.FullName,
                    PhoneNumber = x.PhoneNumber,
                    VehicleType = x.VehicleType,
                    VehicleNumber = x.VehicleNumber,
                    Area = x.Area,
                    Status = x.Status
                })
                .ToList();

            

            _logger.LogInformation("PENDING COUNT = {Count}", Deliveries.Count);
        }

        // =====================
        // APPROVE
        // =====================
        public async Task<IActionResult> OnPostApprove(int id)
        {
            await _deliveryManager.ApproveDeliveryPersonAsync(id);
            return RedirectToPage();
        }

    
        public async Task<IActionResult> OnPostReject(int id)
        {
            await _deliveryManager.RejectDeliveryPersonAsync(id);
            return RedirectToPage();
        }
    }
}