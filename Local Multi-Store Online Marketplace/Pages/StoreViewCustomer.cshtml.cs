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

        public int CustomerId { get; set; }

        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public int LoyaltyPoints { get; set; }
        public bool IsVerified { get; set; }
        public string Gender { get; set; } = "Not Specified";
        public bool CODBlocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsFollowingThisStore { get; set; }
        public int OrdersWithThisStoreCount { get; set; }
        public List<Order> OrdersWithThisStore { get; set; } = new();
        public List<Review> ReviewsForThisStore { get; set; } = new();

        public bool CustomerNotFound { get; set; }
        public bool StoreOwnerHasNoStore { get; set; }
        public bool IsBlocked { get; set; }

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

        private async Task<Store?> GetMyStoreAsync(int ownerUserId)
        {
            Store? myStore = null;
            try
            {
                myStore = await _context.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OwnerUserID == ownerUserId);
            }
            catch
            {
                var stores = await _context.Stores.AsNoTracking().ToListAsync();
                myStore = stores.FirstOrDefault(s =>
                    GetSafeInt(s, new[] { "OwnerUserID", "UserID", "OwnerID" }, -1) == ownerUserId);
            }

            return myStore;
        }

        public async Task<IActionResult> OnGetAsync(int customerId)
        {
            CustomerId = customerId;

            var storeOwnerUser = await _userManager.GetUserAsync(User);

            if (storeOwnerUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var myStore = await GetMyStoreAsync(storeOwnerUser.Id);

            if (myStore == null)
            {
                StoreOwnerHasNoStore = true;
                return Page();
            }

            var storeId = GetSafeInt(myStore, new[] { "StoreID", "Id" }, 0);

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

            OrdersWithThisStore = (customer.Orders ?? new List<Order>())
                .Where(o => GetSafeInt(o, new[] { "StoreID" }, -1) == storeId)
                .ToList();
            OrdersWithThisStoreCount = OrdersWithThisStore.Count;

            ReviewsForThisStore = (customer.Reviews ?? new List<Review>())
                .Where(r => r.StoreID == storeId)
                .ToList();

            IsFollowingThisStore = (customer.FollowedStores ?? new List<StoreFollow>())
                .Any(f => GetSafeInt(f, new[] { "StoreID" }, -1) == storeId);

            if (user != null)
            {
                IsBlocked = await _context.BlockRelations.AnyAsync(b =>
                    b.BlockerUserId == storeOwnerUser.Id &&
                    b.BlockedUserId == user.Id);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostReportCustomerAsync(int customerId, string? reason, string? description)
        {
            var storeOwnerUser = await _userManager.GetUserAsync(User);
            if (storeOwnerUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var myStore = await GetMyStoreAsync(storeOwnerUser.Id);
            if (myStore == null)
                return RedirectToPage(new { customerId });

            var storeId = GetSafeInt(myStore, new[] { "StoreID", "Id" }, 0);

            var customerExists = await _context.Customers.AnyAsync(c => c.CustomerID == customerId);
            if (!customerExists)
                return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please choose a reason for this report.";
                return RedirectToPage(new { customerId });
            }

            _context.Reports.Add(new Report
            {
                ReporterStoreID = storeId,           // adjust to your actual FK name on Report
                TargetType = "Customer",
                TargetId = customerId,
                Reason = reason.Trim(),
                Description = string.IsNullOrWhiteSpace(description)
                    ? "(No additional details provided.)"
                    : description.Trim(),
                Status = "Pending Review",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Your report has been submitted. Our team will review it.";
            return RedirectToPage(new { customerId });
        }

        public async Task<IActionResult> OnPostBlockCustomerAsync(int customerId)
        {
            var storeOwnerUser = await _userManager.GetUserAsync(User);
            if (storeOwnerUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            if (customer == null)
                return NotFound();

            var existing = await _context.BlockRelations.FirstOrDefaultAsync(b =>
                b.BlockerUserId == storeOwnerUser.Id &&
                b.BlockedUserId == customer.UserID);

            if (existing == null)
            {
                _context.BlockRelations.Add(new BlockRelation
                {
                    BlockerUserId = storeOwnerUser.Id,
                    BlockedUserId = customer.UserID,
                    BlockerRole = "StoreOwner",
                    BlockedRole = "Customer",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Customer blocked.";
            return RedirectToPage(new { customerId });
        }
    }
}