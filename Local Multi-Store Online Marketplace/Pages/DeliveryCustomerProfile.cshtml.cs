using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    // Public, read-only profile a customer sees when they tap
    // "View Profile" from the Orders page. Deliberately separate from
    // /DeliveryProfile (the delivery partner's own self-management
    // page) — that page shows private fields (driving license, vehicle
    // number, ID proof, live location) this one must never expose.
    [Authorize(Roles = "Customer")]
    public class DeliveryCustomerProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly DeliveryReviewManager _deliveryReviewManager;

        public DeliveryCustomerProfileModel(
            ApplicationDbContext context,
            DeliveryReviewManager deliveryReviewManager)
        {
            _context = context;
            _deliveryReviewManager = deliveryReviewManager;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public DeliveryPublicProfileViewModel? Profile { get; set; }

        public List<DeliveryReviewDTO> Reviews { get; set; } = new();

        public int ReviewCount => Reviews.Count;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Id = id;

            // Only the fields a customer is allowed to see — no phone
            // number, driving license, vehicle number, ID proof, or live
            // location, none of which are projected here.
            Profile = await _context.DeliveryPersons
                .AsNoTracking()
                .Where(d => d.DeliveryPersonID == id)
                .Select(d => new DeliveryPublicProfileViewModel
                {
                    DeliveryPersonID = d.DeliveryPersonID,
                    FullName = d.FullName,
                    Area = d.Area,
                    VehicleType = d.VehicleType,
                    Rating = d.Rating
                })
                .FirstOrDefaultAsync();

            if (Profile == null)
            {
                return Page();
            }

            var reviews = await _deliveryReviewManager.GetReviewsByDeliveryPersonAsync(id);
            Reviews = reviews.OrderByDescending(r => r.CreatedAt).ToList();

            return Page();
        }

        public class DeliveryPublicProfileViewModel
        {
            public int DeliveryPersonID { get; set; }

            public string FullName { get; set; } = string.Empty;

            public string? Area { get; set; }

            public string? VehicleType { get; set; }

            public decimal Rating { get; set; }

            public string Initial =>
                string.IsNullOrWhiteSpace(FullName)
                    ? "D"
                    : FullName.Trim().Substring(0, 1).ToUpper();
        }
    }
}
