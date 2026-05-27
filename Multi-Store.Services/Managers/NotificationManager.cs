using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class NotificationManager
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationManager(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        private NotificationDTO ToDTO(Notification n)
        {
            return new NotificationDTO
            {
                NotificationID = n.NotificationID,
                UserID = n.UserID,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                ReferenceID = n.ReferenceID,
                IsRead = n.IsRead,
                SentAt = n.SentAt,
                SentVia = n.SentVia,
                User = n.User
            };
        }

        // SEND NOTIFICATION
        public async Task<NotificationDTO> SendAsync(
            int userId,
            string title,
            string message,
            string type,
            int? referenceId = null,
            string sentVia = "System")
        {
            var notification = new Notification
            {
                UserID = userId,
                Title = title,
                Message = message,
                Type = type,
                ReferenceID = referenceId,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = sentVia
            };

            await _notificationRepository.AddAsync(notification);

            return ToDTO(notification);
        }

        // MARK AS READ
        public async Task<NotificationDTO> MarkAsReadAsync(int id)
        {
            var notification = await _notificationRepository.GetByIdAsync(id);

            if (notification == null)
                throw new Exception("Notification not found");

            notification.IsRead = true;

            await _notificationRepository.UpdateAsync(notification);

            return ToDTO(notification);
        }

        // GET USER NOTIFICATIONS
        public async Task<List<NotificationDTO>> GetUserAsync(int userId)
        {
            var list = await _notificationRepository.GetByUserAsync(userId);
            return list.Select(ToDTO).ToList();
        }

        // GET UNREAD
        public async Task<List<NotificationDTO>> GetUnreadAsync(int userId)
        {
            var list = await _notificationRepository.GetUnreadByUserAsync(userId);
            return list.Select(ToDTO).ToList();
        }

        // UNREAD COUNT
        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _notificationRepository.GetUnreadCountAsync(userId);
        }
    }
}
