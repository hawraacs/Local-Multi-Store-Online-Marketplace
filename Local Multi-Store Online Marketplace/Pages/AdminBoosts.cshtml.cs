using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminBoostsModel : PageModel
    {
        private readonly BoostManager _boostManager;

        public AdminBoostsModel(BoostManager boostManager)
        {
            _boostManager = boostManager;
        }

        public List<ProductBoost> Boosts { get; set; } = new();
        public decimal TotalBoostRevenue { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            // Flip any boosts whose EndDate has passed from Active -> Expired
            // before we read/display them, so the list is always current.
            await _boostManager.ExpireDueBoostsAsync();

            Boosts = await _boostManager.GetAllBoostsAsync(StatusFilter);
            TotalBoostRevenue = await _boostManager.GetTotalBoostRevenueAsync();
        }
    }
}