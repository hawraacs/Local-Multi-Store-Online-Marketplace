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
    public class AdminNotificationsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public AdminNotificationsModel(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /AdminNotifications?handler=List
        // Returns the current admin's most recent notifications as JSON.
        public async Task<JsonResult> OnGetListAsync()
        {
            var uid = GetCurrentAdminId();
            if (uid == null)
            {
                return new JsonResult(new { items = Array.Empty<object>(), unreadCount = 0 });
            }

            var items = await _db.Notifications
                .Where(n => n.UserID == uid)
                .OrderByDescending(n => n.SentAt)
                .Take(20)
                .Select(n => new
                {
                    n.NotificationID,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.ReferenceID,
                    n.IsRead,
                    SentAt = n.SentAt
                })
                .ToListAsync();

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.UserID == uid && !n.IsRead);

            return new JsonResult(new { items, unreadCount });
        }

        // POST /AdminNotifications?handler=MarkRead
        // Marks a single notification as read (called when the admin opens/clicks it)
        // and returns the fresh unread count so the topbar badge can update immediately.
        public async Task<JsonResult> OnPostMarkReadAsync([FromForm] int id)
        {
            var uid = GetCurrentAdminId();
            if (uid == null)
            {
                return new JsonResult(new { success = false });
            }

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == uid);

            // Either the notification doesn't exist or it doesn't belong to this admin -
            // don't leak whether the id exists, just report failure either way.
            if (notification == null)
            {
                return new JsonResult(new { success = false });
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.UserID == uid && !n.IsRead);

            return new JsonResult(new { success = true, unreadCount });
        }

        // POST /AdminNotifications?handler=MarkAllRead
        public async Task<JsonResult> OnPostMarkAllReadAsync()
        {
            var uid = GetCurrentAdminId();
            if (uid == null)
            {
                return new JsonResult(new { success = false });
            }

            var unread = await _db.Notifications
                .Where(n => n.UserID == uid && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _db.SaveChangesAsync();

            return new JsonResult(new { success = true, unreadCount = 0 });
        }

        // Fallback so the page doesn't error if ever hit directly without a handler.
        public IActionResult OnGet() => new EmptyResult();

        private int? GetCurrentAdminId()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null || !int.TryParse(userId, out var uid))
            {
                return null;
            }
            return uid;
        }
    }
}
