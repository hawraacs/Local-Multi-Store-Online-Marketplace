using System;

namespace Multi_Store.Services.Dtos
{
    public class AuditLogDTO 
    {
        public int AuditLogID { get; set; }
        public int UserID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityID { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
    }
}