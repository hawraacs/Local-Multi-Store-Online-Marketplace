using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    // NOTE: confirm this matches the exact role name assigned to store-owner
    // accounts in your Identity setup (based on your Pages/StoreOwner folder,
    // "StoreOwner" is the most likely candidate).
    [Authorize(Roles = "StoreOwner")]
    public class StoreViewCustomerModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public StoreViewCustomerModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ==========================================
        // Basic customer info (read-only, no [BindProperty])
        // ==========================================
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public int LoyaltyPoints { get; set; }
        public bool IsVerified { get; set; }
        public string Gender { get; set; } = "Not Specified";
        public bool CODBlocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Scoped to THIS store only
        public bool IsFollowingThisStore { get; set; }
        public int OrdersWithThisStoreCount { get; set; }
        public List<Order> OrdersWithThisStore { get; set; } = new();
        public List<Review> ReviewsForThisStore { get; set; } = new();

        public bool CustomerNotFound { get; set; }
        public bool StoreOwnerHasNoStore { get; set; }

        // Safe reflection-based lookups (same pattern used in CustomerProfileModel)
        public static string GetSafeString(object? obj, string[] propertyNames, string defaultValue = "")
        {
            if (obj == null) return defaultValue;
            var type = obj.GetType();
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null) return val.ToString() ?? defaultValue;
                }
            }
            return defaultValue;
        }

        public static int GetSafeInt(object? obj, string[] propertyNames, int defaultValue = 0)
        {
            if (obj == null) return defaultValue;
            var type = obj.GetType();
            foreach (var name in propertyNames)
            {
                var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val != null && int.TryParse(val.ToString(), out int res)) return res;
                }
            }
            return defaultValue;
        }

        public async Task<IActionResult> OnGetAsync(int customerId)
        {
            var storeOwnerUser = await _userManager.GetUserAsync(User);

            if (storeOwnerUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // 1. Resolve which store this owner manages.
            // Adjust "OwnerUserID" below if your Store entity names this field differently.
            Store? myStore = null;
            try
            {
                myStore = await _context.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OwnerUserID == storeOwnerUser.Id);
            }
            catch
            {
                // Fallback: try a common alternate property name via reflection
                var stores = await _context.Stores.AsNoTracking().ToListAsync();
                myStore = stores.FirstOrDefault(s =>
                    GetSafeInt(s, new[] { "OwnerUserID", "UserID", "OwnerID" }, -1) == storeOwnerUser.Id);
            }

            if (myStore == null)
            {
                StoreOwnerHasNoStore = true;
                return Page();
            }

            var storeId = GetSafeInt(myStore, new[] { "StoreID", "Id" }, 0);

            // 2. Load the target customer, including only what we need.
            Customer? customer = null;
            try
            {
                customer = await _context.Customers
                    .Include(c => c.User)
                    .Include(c => c.Orders)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.Product)
                    .Include(c => c.FollowedStores)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == customerId);
            }
            catch
            {
                customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == customerId);
            }

            if (customer == null)
            {
                CustomerNotFound = true;
                return Page();
            }

            var user = customer.User ?? await _userManager.FindByIdAsync(customer.UserID.ToString());

            CustomerFullName = !string.IsNullOrWhiteSpace(user?.FullName)
                ? user!.FullName
                : user?.UserName ?? "Customer";

            CustomerEmail = user?.Email ?? string.Empty;
            CustomerPhone = user?.PhoneNumber ?? "No phone number";

            LoyaltyPoints = customer.LoyaltyPoints;
            IsVerified = customer.IsVerified;
            Gender = customer.Gender ?? "Not Specified";
            CODBlocked = customer.CODBlocked;
            CreatedAt = customer.CreatedAt;

            // 3. Scope orders/reviews/follow status to THIS store only.
            // Orders: assumes Order has a StoreID property. If orders are
            // actually scoped per-line-item instead, replace this filter
            // with a join through OrderItems -> Product -> StoreID.
            OrdersWithThisStore = (customer.Orders ?? new List<Order>())
                .Where(o => GetSafeInt(o, new[] { "StoreID" }, -1) == storeId)
                .ToList();
            OrdersWithThisStoreCount = OrdersWithThisStore.Count;

            // Review carries StoreID directly, so filter on that.
            ReviewsForThisStore = (customer.Reviews ?? new List<Review>())
                .Where(r => r.StoreID == storeId)
                .ToList();

            IsFollowingThisStore = (customer.FollowedStores ?? new List<StoreFollow>())
                .Any(f => GetSafeInt(f, new[] { "StoreID" }, -1) == storeId);

            return Page();
        }
    }
}