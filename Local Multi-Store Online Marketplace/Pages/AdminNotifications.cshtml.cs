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

        // NotificationDTO has the same property names as the Notification entity
        // (NotificationID, Title, Message, Type, IsRead, SentAt), so the view
        // markup from before still works unchanged against this DTO.
        public List<NotificationDTO> Notifications { get; set; } = new();

        public int TotalCount => Notifications.Count;
        public int UnreadCount => Notifications.Count(n => !n.IsRead);
        public int TodayCount => Notifications.Count(n => n.SentAt.Date == DateTime.UtcNow.Date);

        // =====================================================
        // FULL PAGE (direct visits to /AdminNotifications)
        // =====================================================
        public async Task OnGetAsync()
        {
            var currentUserId = GetCurrentUserId();

            var all = await _notifications.GetUserAsync(currentUserId);
            Notifications = all.OrderByDescending(n => n.SentAt).ToList();
        }

        // =====================================================
        // JSON LIST — used only by the topbar bell dropdown in the admin layout.
        // GET /AdminNotifications?handler=List
        // =====================================================
        public async Task<IActionResult> OnGetListAsync()
        {
            var currentUserId = GetCurrentUserId();

            var all = await _notifications.GetUserAsync(currentUserId);
            var recent = all.OrderByDescending(n => n.SentAt).Take(10);
            var unreadCount = await _notifications.GetUnreadCountAsync(currentUserId);

            return new JsonResult(new
            {
                items = recent.Select(n => new
                {
                    notificationID = n.NotificationID,
                    type = n.Type,
                    referenceID = n.ReferenceID,
                    title = n.Title,
                    message = n.Message,
                    isRead = n.IsRead,
                    sentAt = n.SentAt
                }),
                unreadCount
            });
        }

        // =====================================================
        // MARK ONE AS READ — used by the FULL PAGE's own <form> (native post-back,
        // browser navigates, so this must redirect, not return JSON).
        // POST /AdminNotifications?handler=MarkRead
        // =====================================================
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

        // =====================================================
        // MARK ALL AS READ — same story, full page's own <form>.
        // POST /AdminNotifications?handler=MarkAllRead
        // =====================================================
        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var currentUserId = GetCurrentUserId();
            await _notifications.MarkAllAsReadAsync(currentUserId);

            return RedirectToPage();
        }

        // =====================================================
        // MARK ONE AS READ (JSON) — used by the topbar bell's fetch() call.
        // POST /AdminNotifications?handler=MarkReadApi
        // =====================================================
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

        // =====================================================
        // MARK ALL AS READ (JSON) — used by the topbar bell's fetch() call.
        // POST /AdminNotifications?handler=MarkAllReadApi
        // =====================================================
        public async Task<IActionResult> OnPostMarkAllReadApiAsync()
        {
            var currentUserId = GetCurrentUserId();
            await _notifications.MarkAllAsReadAsync(currentUserId);

            return new JsonResult(new { success = true, unreadCount = 0 });
        }

        private int GetCurrentUserId()
        {
            return int.Parse(_userManager.GetUserId(base.User)!);
        }
    }
}
