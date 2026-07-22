using System;

namespace Multi_Store.Core.Entities
{
    public class ProductBoost
    {
        public int ProductBoostID { get; set; }

        public int ProductID { get; set; }
        public int StoreID { get; set; }

        public int DurationDays { get; set; }
        public decimal AmountPaid { get; set; }

        // PendingPayment -> Active -> Expired  (or Cancelled)
        public string Status { get; set; } = "PendingPayment";

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Payment trail — mirrors your subscription payment pattern
        public string? StripePaymentIntentId { get; set; }
        public int? StorePaymentId { get; set; }

        public virtual Product Product { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
        public virtual StorePayment? StorePayment { get; set; }
    }
}