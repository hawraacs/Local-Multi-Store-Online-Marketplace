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
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "StoreOwner")]
    public class StoreViewCustomerModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ReviewManager _reviewManager;

        public StoreViewCustomerModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            ReviewManager reviewManager)
        {
            _userManager = userManager;
            _context = context;
            _reviewManager = reviewManager;
        }

        public int CustomerId { get; set; }

        // NEW — the store owner's own StoreID, needed to link a review row
        // back to its place on the main Store Reviews page.
        public int StoreId { get; set; }

        // NEW — the customer's UserID (distinct from CustomerID), needed
        // to route the "Message" button to /ChatConversation?userId=...
        public int CustomerUserId { get; set; }

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
            StoreId = storeId;

            Customer? customer = null;
            try
            {
                customer = await _context.Customers
                    .Include(c => c.User)
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

            CustomerUserId = customer.UserID;

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

            // FIX — "Orders With Your Store" was always empty. Order rows
            // don't carry a StoreID themselves (only OrderItems do — that's
            // how multi-vendor orders work), so the previous reflection
            // lookup for o.StoreID always fell back to -1 and matched
            // nothing. This now resolves store orders the same way the
            // Store Owner's Orders/Index page already does: find every
            // OrderID that has at least one OrderItem for this store, then
            // load this customer's Orders that are in that set.
            var storeOrderIdsQuery = _context.OrderItems
                .Where(oi => oi.StoreID == storeId)
                .Select(oi => oi.OrderID)
                .Distinct();

            OrdersWithThisStore = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerID == customerId && storeOrderIdsQuery.Contains(o.OrderID))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            OrdersWithThisStoreCount = OrdersWithThisStore.Count;

            ReviewsForThisStore = (customer.Reviews ?? new List<Review>())
                .Where(r => r.StoreID == storeId)
                .OrderByDescending(r => r.CreatedAt)
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

            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);
            if (customer == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please choose a reason for this report.";
                return RedirectToPage(new { customerId });
            }

            var report = new Report
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
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            // =====================================================
            // NOTIFY ALL ADMINS — reports were being saved but nobody
            // was ever told one came in. Same pattern used for the
            // Complaint-based reports in StoreCustomerProfileModel.
            // =====================================================
            var customerLabel = !string.IsNullOrWhiteSpace(customer.User?.FullName)
                ? customer.User!.FullName
                : $"Customer #{customerId}";

            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            foreach (var admin in admins)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = admin.Id,
                    Title = "New report filed",
                    Message = $"A store owner reported customer \"{customerLabel}\" for \"{report.Reason}\".",
                    Type = "Report",
                    ReferenceID = report.ReportID,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });
            }

            if (admins.Any())
            {
                await _context.SaveChangesAsync();
            }

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

        // ==========================================
        // NEW — lets a store owner remove a review left on their store /
        // their products, right from this customer profile (double-click
        // or the trash icon on a review row). Scoped to reviews that
        // actually belong to this store owner's store, so a store owner
        // can't delete a review on a different store by guessing an id.
        // Uses the same ReviewManager.DeleteReviewAsync path (with
        // IP/User-Agent audit logging) as the main Store Reviews page.
        // ==========================================
        public async Task<IActionResult> OnPostDeleteReviewAsync(int customerId, int reviewId)
        {
            var storeOwnerUser = await _userManager.GetUserAsync(User);
            if (storeOwnerUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var myStore = await GetMyStoreAsync(storeOwnerUser.Id);
            if (myStore == null)
                return RedirectToPage(new { customerId });

            var storeId = GetSafeInt(myStore, new[] { "StoreID", "Id" }, 0);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ReviewID == reviewId && r.StoreID == storeId);

            if (review == null)
            {
                TempData["Error"] = "That review couldn't be found on your store.";
                return RedirectToPage(new { customerId });
            }

            await _reviewManager.DeleteReviewAsync(
                reviewId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Headers["User-Agent"].ToString()
            );

            TempData["Success"] = "Review deleted.";
            return RedirectToPage(new { customerId });
        }
    }
}