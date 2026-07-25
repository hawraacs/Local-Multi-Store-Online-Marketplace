using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminNotificationsModel : PageModel
    {
        private readonly NotificationManager _notifications;
        private readonly UserManager<User> _userManager;

        public AdminNotificationsModel(NotificationManager notifications, UserManager<User> userManager)
        {
            _notifications = notifications;
            _userManager = userManager;
        }

        public List<NotificationDTO> Notifications { get; set; } = new();

        public int TotalCount => Notifications.Count;
        public int UnreadCount => Notifications.Count(n => !n.IsRead);
        public int TodayCount => Notifications.Count(n => n.SentAt.Date == DateTime.UtcNow.Date);

        private int GetCurrentUserId()
        {
            var userId = _userManager.GetUserId(User);
            return int.TryParse(userId, out var uid) ? uid : 0;
        }

        public async Task OnGetAsync()
        {
            var currentUserId = GetCurrentUserId();
            var all = await _notifications.GetUserAsync(currentUserId);
            Notifications = all.OrderByDescending(n => n.SentAt).ToList();
        }

        public async Task<IActionResult> OnGetListAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null || !int.TryParse(userId, out var uid))
            {
                return new JsonResult(new { items = Array.Empty<object>(), unreadCount = 0 });
            }

            var all = await _notifications.GetUserAsync(uid);
            var recent = all.OrderByDescending(n => n.SentAt).Take(10).ToList();
            var unreadCount = all.Count(n => !n.IsRead);

            var items = recent.Select(n => new
            {
                notificationID = n.NotificationID,
                type = n.Type,
                referenceID = n.ReferenceID,
                title = n.Title,
                message = n.Message,
                isRead = n.IsRead,
                sentAt = n.SentAt
            });

            return new JsonResult(new { items, unreadCount });
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            var currentUserId = GetCurrentUserId();
            var mine = await _notifications.GetUserAsync(currentUserId);
            if (mine.Any(n => n.NotificationID == id))
            {
                await _notifications.MarkAsReadAsync(id);
            }
            return RedirectToPage();
        }

        public async Task<JsonResult> OnPostMarkAllReadAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null || !int.TryParse(userId, out var uid))
            {
                return new JsonResult(new { success = false });
            }

            await _notifications.MarkAllAsReadAsync(uid);
            return new JsonResult(new { success = true, unreadCount = 0 });
        }

        public async Task<IActionResult> OnPostMarkReadApiAsync(int id)
        {
            var currentUserId = GetCurrentUserId();
            var mine = await _notifications.GetUserAsync(currentUserId);
            if (mine.Any(n => n.NotificationID == id))
            {
                await _notifications.MarkAsReadAsync(id);
            }

            var unreadCount = await _notifications.GetUnreadCountAsync(currentUserId);
            return new JsonResult(new { success = true, unreadCount });
        }

        public async Task<IActionResult> OnPostMarkAllReadApiAsync()
        {
            var currentUserId = GetCurrentUserId();
            await _notifications.MarkAllAsReadAsync(currentUserId);
            return new JsonResult(new { success = true, unreadCount = 0 });
        }

        public IActionResult OnGet() => new EmptyResult();
    }
}