using System;

namespace Multi_Store.Core.Entities
{
    public class PasswordResetOtp
    {
        public int PasswordResetOtpID { get; set; }

        public int UserID { get; set; }

        public string DeliveryMethod { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string OtpHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime? UsedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}