using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminSearchModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public AdminSearchModel(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public List<UserResult> UserResults { get; set; } = new();
        public List<OrderResult> OrderResults { get; set; } = new();

        // TODO: Store search — needs Store.cs to confirm the searchable
        // property (e.g. StoreName). Uncomment and adjust once available:
        //
        // public List<StoreResult> StoreResults { get; set; } = new();
        //
        // StoreResults = await _db.Stores
        //     .Where(s => s.StoreName.Contains(Q))
        //     .Select(s => new StoreResult
        //     {
        //         StoreID = s.StoreID,
        //         StoreName = s.StoreName
        //     })
        //     .Take(10)
        //     .ToListAsync();

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Q))
            {
                return;
            }

            var query = Q.Trim();

            UserResults = await _userManager.Users
                .Where(u => u.Email!.Contains(query) || u.UserName!.Contains(query))
                .Take(10)
                .Select(u => new UserResult
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    UserName = u.UserName ?? ""
                })
                .ToListAsync();

            OrderResults = await _db.Orders
                .Where(o => o.OrderNumber.Contains(query))
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new OrderResult
                {
                    OrderID = o.OrderID,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    CustomerID = o.CustomerID
                })
                .ToListAsync();
        }

        public class UserResult
        {
            public int Id { get; set; }
            public string Email { get; set; } = "";
            public string UserName { get; set; } = "";
        }

        public class OrderResult
        {
            public int OrderID { get; set; }
            public string OrderNumber { get; set; } = "";
            public DateTime OrderDate { get; set; }
            public int CustomerID { get; set; }
        }
    }
}
