using System;

namespace Multi_Store.Core.Entities
{
    public class Session
    {
        public int SessionID { get; set; }
        public int UserID { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}