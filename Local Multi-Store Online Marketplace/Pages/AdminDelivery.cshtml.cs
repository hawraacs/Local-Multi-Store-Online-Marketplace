using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminDeliveryModel : PageModel
    {
        private readonly DeliveryManager _deliveryManager;
        private readonly ILogger<AdminDeliveryModel> _logger;

        public AdminDeliveryModel(
            DeliveryManager deliveryManager,
            ILogger<AdminDeliveryModel> logger)
        {
            _deliveryManager = deliveryManager;
            _logger = logger;
        }

        public List<DeliveryPersonDTO> Deliveries { get; set; } = new();

        public async Task OnGetAsync()
        {
            Deliveries = (await _deliveryManager.GetAllAsync())
                .OrderByDescending(d => d.DeliveryPersonID)
                .ToList();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            try
            {
                await _deliveryManager.ApproveDeliveryPersonAsync(id);
                TempData["Success"] = "Delivery request approved.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving delivery request with ID {Id}", id);
                TempData["Error"] = "An error occurred while approving the delivery request.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            try
            {
                await _deliveryManager.RejectDeliveryPersonAsync(id, reason);
                TempData["Success"] = "Delivery request rejected.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting delivery request with ID {Id}", id);
                TempData["Error"] = "An error occurred while rejecting the delivery request.";
            }

            return RedirectToPage();
        }
    }
}