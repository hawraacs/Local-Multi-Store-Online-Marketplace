using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CreateReviewModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CreateReviewModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public int StoreId { get; set; }

        [BindProperty]
        public int Rating { get; set; }

        [BindProperty]
        public string Comment { get; set; }

        public List<Review> Reviews { get; set; } = new();

        // LOAD REVIEWS
        public async Task OnGetAsync(int storeId)
        {
            StoreId = storeId;

            Reviews = await _context.Reviews
                .Include(r => r.Customer)
                    .ThenInclude(c => c.User)
                .Where(r => r.StoreID == storeId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        // SUBMIT REVIEW
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            // CHANGED — now includes User so the notification message below
            // can name the actual customer instead of saying "A customer".
            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage("/Customer1");

            if (Rating < 1 || Rating > 5)
            {
                TempData["Error"] = "Please give a rating between 1 and 5.";
                return RedirectToPage(new { storeId = StoreId });
            }

            var cleanComment = Comment?.Trim();
            if (string.IsNullOrWhiteSpace(cleanComment))
            {
                TempData["Error"] = "Please write a review.";
                return RedirectToPage(new { storeId = StoreId });
            }

            var review = new Review
            {
                CustomerID = customer.CustomerID,
                StoreID = StoreId,
                Rating = Rating,
                Comment = cleanComment,
                Status = "Approved", // explicit, consistent with every other review-creation path
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync(); // saved first so review.ReviewID is populated below

            // Recompute the store's aggregate rating/total from all
            // non-product (store-level), non-rejected reviews, mirroring
            // the pattern already used for product ratings elsewhere.
            var storeReviews = await _context.Reviews
                .Where(r => r.StoreID == StoreId && r.ProductID == null && r.Status != "Rejected")
                .ToListAsync();

            var store = await _context.Stores.FindAsync(StoreId);
            if (store != null)
            {
                store.TotalRatings = storeReviews.Count;
                store.Rating = storeReviews.Count > 0
                    ? Math.Round((decimal)storeReviews.Average(r => r.Rating), 2)
                    : 0m;

                // CHANGED — now names the customer and includes a short
                // excerpt of what they actually wrote, since Notification
                // has no separate columns for these — Message is the only
                // place this information can live for the view to show.
                var customerName = !string.IsNullOrWhiteSpace(customer.User?.FullName)
                    ? customer.User.FullName
                    : (customer.User?.UserName ?? "A customer");

                var excerpt = cleanComment.Length > 80
                    ? cleanComment[..80] + "..."
                    : cleanComment;

                _context.Notifications.Add(new Notification
                {
                    UserID = store.OwnerUserID,
                    Title = "New store review",
                    Message = $"{customerName} left a {Rating}-star review on your store: \"{excerpt}\"",
                    Type = "StoreReview",
                    ReferenceID = review.ReviewID,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Thanks for your review!";
            return RedirectToPage(new { storeId = StoreId });
        }
    }
}