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

        // ? ADD THIS: reviews list for UI
        public List<Review> Reviews { get; set; } = new();

        // ? LOAD REVIEWS
        public async Task OnGetAsync(int storeId)
        {
            StoreId = storeId;

            Reviews = await _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.StoreID == storeId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        // ? SUBMIT REVIEW
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
                return RedirectToPage("/Customer1");

            _context.Reviews.Add(new Review
            {
                CustomerID = customer.CustomerID,
                StoreID = StoreId,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToPage(new { storeId = StoreId });
        }
    }
}