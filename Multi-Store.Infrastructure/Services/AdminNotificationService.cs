using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Multi_Store.Infrastructure.Services
{
    public interface IAdminNotificationService
    {
        /// <summary>
        /// Creates a notification for every user currently in the "Admin" role.
        /// `type` MUST be one of: "complaint", "delivery", "store", "user", "order"
        /// to match the icon/link mapping in the admin topbar's JS
        /// (notifTypeMeta / notifLinkFor in _Layout.cshtml). Anything else falls
        /// back to a generic bell icon linking to /AdminReports.
        /// </summary>
        Task NotifyAdminsAsync(string title, string message, string type, int? referenceId = null);
    }

    public class AdminNotificationService : IAdminNotificationService
    {
        private const string AdminRoleName = "Admin";

        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public AdminNotificationService(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task NotifyAdminsAsync(string title, string message, string type, int? referenceId = null)
        {
            var admins = await _userManager.GetUsersInRoleAsync(AdminRoleName);
            if (admins == null || admins.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;

            var notifications = admins.Select(admin => new Notification
            {
                UserID = admin.Id,
                Title = title,
                Message = message,
                Type = type,
                ReferenceID = referenceId,
                IsRead = false,
                SentAt = now,
                SentVia = "InApp"
            });

            _db.Notifications.AddRange(notifications);
            await _db.SaveChangesAsync();
        }
    }
}
