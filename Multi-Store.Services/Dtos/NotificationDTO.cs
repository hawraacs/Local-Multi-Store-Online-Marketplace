using System;

namespace Multi_Store.Services.Dtos
{
    public class NotificationDTO
    {
        // Primary Key
        public int NotificationID { get; set; }

        // Foreign Key
        public int UserID { get; set; }

        // Attributes
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public int? ReferenceID { get; set; }

        public bool IsRead { get; set; }

        public DateTime SentAt { get; set; }

        public string SentVia { get; set; } = string.Empty;

        // Relationships

        // Many Notifications belong to one User
        public User? User { get; set; }
    }
}