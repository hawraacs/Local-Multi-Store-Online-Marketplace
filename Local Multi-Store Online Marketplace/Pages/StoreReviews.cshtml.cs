using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using Microsoft.Extensions.Logging;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class StoreReviewsModel : PageModel
    {
        private readonly ReviewManager _reviewManager;
        private readonly ILogger<StoreReviewsModel> _logger;
        public StoreReviewsModel(ReviewManager reviewManager, ILogger<StoreReviewsModel> logger)
        {
            _reviewManager = reviewManager;
            _logger = logger;

        }

        public List<ReviewDTO> Reviews { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int StoreId { get; set; }

        public async Task OnGetAsync(int storeId)
        {
            StoreId = storeId;
            ViewData["StoreId"] = StoreId;

            _logger.LogInformation("STORE REVIEWS PAGE LOADED. StoreId = {StoreId}", StoreId);

            var result = await _reviewManager.GetReviewsByStoreAsync(StoreId);

            Reviews = result.ToList();

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

            _logger.LogInformation("REVIEW DELETED SUCCESSFULLY. ReviewId = {ReviewId}", reviewId);

            return RedirectToPage(new { storeId = StoreId });
        }
    }
}