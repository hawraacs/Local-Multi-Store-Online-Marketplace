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

        // =====================================================
        // FULL PAGE (direct visits to /AdminNotifications)
        // NOTE: this is the ONLY GET handler on this page. A stray
        // "public IActionResult OnGet() => new EmptyResult();" got
        // merged in alongside this one, which is what caused
        // "Multiple handlers matched" - do not re-add a second OnGet.
        // =====================================================
        public async Task OnGetAsync()
        {
            var currentUserId = GetCurrentUserId();
            var all = await _notifications.GetUserAsync(currentUserId);
            Notifications = all.OrderByDescending(n => n.SentAt).ToList();
        }

        // =====================================================
        // JSON LIST — used only by the topbar bell dropdown.
        // GET /AdminNotifications?handler=List
        // =====================================================
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

        // =====================================================
        // MARK ONE AS READ — used by the FULL PAGE's own <form> (native
        // post-back, browser navigates, so this redirects, not JSON).
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
        // FIX: this had been changed to return JsonResult, which breaks
        // a normal HTML form post (the browser would just display raw
        // JSON instead of the page). Restored to RedirectToPage().
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
    }
}