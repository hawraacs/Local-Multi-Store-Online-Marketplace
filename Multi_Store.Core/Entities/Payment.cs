using System;

namespace Multi_Store.Core.Entities
{
    public class Payment
    {
        public int PaymentID { get; set; }
        public int OrderID { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentGateway { get; set; } = string.Empty;
        public string? GatewayTransactionID { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public decimal? RefundAmount { get; set; }
        public DateTime? RefundDate { get; set; }

        // ⚠️ REQUIRED - Navigation property
        public virtual Order Order { get; set; } = null!;
    }
}