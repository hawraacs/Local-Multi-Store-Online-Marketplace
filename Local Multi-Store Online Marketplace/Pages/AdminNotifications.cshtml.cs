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
            var userId = _userManager.GetUserId(User);
            if (userId == null || !int.TryParse(userId, out var uid))
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

        // POST /AdminNotifications?handler=MarkAllRead
        public async Task<JsonResult> OnPostMarkAllReadAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null || !int.TryParse(userId, out var uid))
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

            return new JsonResult(new { success = true });
        }

        // Fallback so the page doesn't error if ever hit directly without a handler.
        public IActionResult OnGet() => new EmptyResult();
    }
}
