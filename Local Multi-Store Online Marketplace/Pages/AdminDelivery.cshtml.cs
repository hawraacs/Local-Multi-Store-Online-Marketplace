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
            await LoadDeliveriesAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            try
            {
                var result = await _deliveryManager.ApproveDeliveryPersonAsync(id);

                TempData["Success"] =
                    $"Delivery request approved successfully. Email: {result.email} | Default Password: {result.password}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving delivery request with ID {Id}", id);
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            try
            {
                await _deliveryManager.RejectDeliveryPersonAsync(id, reason);

                TempData["Success"] =
                    "Delivery request rejected successfully. No delivery account was created.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting delivery request with ID {Id}", id);
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostActivateAsync(int id)
        {
            try
            {
                await _deliveryManager.ActivateDeliveryPersonAsync(id);
                TempData["Success"] = "Delivery staff activated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating delivery staff with ID {Id}", id);
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(int id)
        {
            try
            {
                await _deliveryManager.DeactivateDeliveryPersonAsync(id);
                TempData["Success"] = "Delivery staff deactivated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating delivery staff with ID {Id}", id);
                TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
            }

            return RedirectToPage();
        }
        private async Task LoadDeliveriesAsync()
        {
            Deliveries = (await _deliveryManager.GetAllAsync())
                .OrderByDescending(d =>
                    d.Status == "Pending" ? 3 :
                    d.Status == "Approved" ? 2 :
                    d.Status == "Rejected" ? 1 : 0)
                .ThenByDescending(d => d.DeliveryPersonID)
                .ToList();
        }
    }
}