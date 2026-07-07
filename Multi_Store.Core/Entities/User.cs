using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class User : IdentityUser<int>
    {
        
       
        public string FullName { get; set; } = string.Empty;
       
        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties - MAKE SURE ALL EXIST
       

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