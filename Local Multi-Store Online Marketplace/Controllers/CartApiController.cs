using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Controllers
{
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    public class CartApiController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public CartApiController(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Ok(new { count = 0 });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                return Ok(new { count = 0 });
            }

            var count = await _context.CartItems
                .Where(ci => ci.Cart.CustomerID == customer.CustomerID)
                .CountAsync();

            return Ok(new { count });
        }
    }
}