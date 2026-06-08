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
    public class CreateProductReviewModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CreateProductReviewModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        [BindProperty]

        public int ProductId { get; set; }

        [BindProperty]
        public int Rating { get; set; }

        [BindProperty]
        public string Comment { get; set; }

        public void OnGet(int productId)
        {
            ProductId = productId;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            var customer = _context.Customers
                .FirstOrDefault(c => c.UserID == user.Id);
            

            if (customer == null)
                return RedirectToPage("/Customer1");
            var product = await _context.Products
    .FirstOrDefaultAsync(p => p.ProductID == ProductId);
            if (product == null)
                return RedirectToPage("/Customer1");

            _context.Reviews.Add(new Review
            {
                CustomerID = customer.CustomerID,
                ProductID = ProductId,
                Rating = Rating,
                StoreID = product.StoreID,
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToPage("/Customer1");
        }
    }
}