using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
using Microsoft.Extensions.Logging;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreReviewsModel : PageModel
    {
        private readonly ReviewManager _reviewManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StoreReviewsModel> _logger;

        public StoreReviewsModel(
            ReviewManager reviewManager,
            ApplicationDbContext context,
            ILogger<StoreReviewsModel> logger)
        {
            _reviewManager = reviewManager;
            _context = context;
            _logger = logger;
        }

        // CHANGED — was List<ReviewDTO> sourced from
        // ReviewManager.GetReviewsByStoreAsync. That path's CustomerName
        // (built via AutoMapper) turned out to be wrong for a customer who
        // is also a Store Owner: it was resolving to the customer's OWN
        // store name instead of their actual name. CreateReviewModel (the
        // "leave a review" page) never had this bug, because it reads the
        // name straight off the entity relationship — Customer.User.FullName
        // — instead of going through the DTO. This page now does the same:
        // queries the Review entities directly with Customer.User included,
        // and the view reads the name the exact same way CreateReview does.
        public List<Review> Reviews { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int StoreId { get; set; }

        public async Task OnGetAsync(int storeId)
        {
            StoreId = storeId;
            ViewData["StoreId"] = StoreId;

            _logger.LogInformation("STORE REVIEWS PAGE LOADED. StoreId = {StoreId}", StoreId);

            // Shop-level reviews only (ProductID == null) — see the earlier
            // fix that excluded product reviews, which also carry the
            // store's StoreID and would otherwise show up here too.
            Reviews = await _context.Reviews
                .Include(r => r.Customer)
                    .ThenInclude(c => c.User)
                .Where(r => r.StoreID == StoreId && r.ProductID == null)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("REVIEWS COUNT = {Count}", Reviews.Count);

            foreach (var r in Reviews)
            {
                _logger.LogInformation(
                    "ReviewID={ReviewID}, Rating={Rating}, CustomerID={CustomerID}",
                    r.ReviewID, r.Rating, r.CustomerID
                );
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int reviewId)
        {
            _logger.LogInformation("DELETE REQUEST RECEIVED. ReviewId = {ReviewId}", reviewId);

            if (reviewId <= 0)
            {
                _logger.LogWarning("INVALID REVIEW ID");
                return RedirectToPage(new { storeId = StoreId });
            }

            await _reviewManager.DeleteReviewAsync(
                reviewId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Headers["User-Agent"].ToString()
            );
            TempData["SuccessMessage"] = "Review deleted successfully!";
            _logger.LogInformation("REVIEW DELETED SUCCESSFULLY. ReviewId = {ReviewId}", reviewId);

            return RedirectToPage(new { storeId = StoreId });
        }
    }
}