using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class AdminDeliveryModel : PageModel
    {
        private readonly DeliveryManager _deliveryManager;
        private readonly IDeliveryPersonRepository _deliveryRepo;

        public AdminDeliveryModel(
            DeliveryManager deliveryManager,
            IDeliveryPersonRepository deliveryRepo)
        {
            _deliveryManager = deliveryManager;
            _deliveryRepo = deliveryRepo;
        }

        public List<DeliveryPerson> PendingDelivery { get; set; } = new();
        public List<DeliveryPerson> ActiveDelivery { get; set; } = new();

        public async Task OnGetAsync()
        {
            var all = await _deliveryRepo.GetAllAsync();

            PendingDelivery = all
                .Where(x => x.Status == "Pending")
                .ToList();

            ActiveDelivery = all
                .Where(x => x.Status == "Available")
                .ToList();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var delivery = await _deliveryRepo.GetByIdAsync(id);

            if (delivery != null)
            {
                delivery.Status = "Available";
                await _deliveryRepo.UpdateAsync(delivery);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            var delivery = await _deliveryRepo.GetByIdAsync(id);

            if (delivery != null)
            {
                delivery.Status = "Rejected";
                await _deliveryRepo.UpdateAsync(delivery);
            }

            return RedirectToPage();
        }
    }
}
