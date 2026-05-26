using System;

namespace Multi_Store.Services.Dtos
{
    public class OrderStatusHistory
    {
        public int StatusHistoryID { get; set; }
        public int OrderID { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        // ⚠️ REQUIRED - Navigation property
        public virtual Order Order { get; set; } = null!;
    }
}