using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class User
    {
        public int UserID { get; set; }
        public int RoleID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties - MAKE SURE ALL EXIST
        public virtual Role Role { get; set; } = null!;

        // ⚠️ Customer property
        public virtual Customer? Customer { get; set; }

        // ⚠️ Store property
        public virtual Store? Store { get; set; }

        // ⚠️ DeliveryPerson property
        public virtual DeliveryPerson? DeliveryPerson { get; set; }

        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public virtual ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}