using System;

namespace Multi_Store.Core.Entities
{
    public class OtpCode
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSentAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiryTime { get; set; }

        public bool IsUsed { get; set; } = false;

        public int AttemptCount { get; set; } = 0;

        public virtual User User { get; set; }
        public int FailedOtpAttempts { get; set; }
public bool IsLocked { get; set; }
    }
}