using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Core.Entities
{
    public class Promotion
    {
        public int PromotionID { get; set; }

        public int StoreID { get; set; }

        public int CreatedByUserID { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string AudienceType { get; set; } = "AllCustomers";

        [MaxLength(50)]
        public string? CouponCode { get; set; }

        // ================= AUTOMATIC SALE (no-coupon promotions only) =================
        // Populated only when this promotion was created WITHOUT a coupon and a
        // specific product was selected for it. When CouponCode is set (the existing
        // coupon-required flow), none of these are used — that flow is unchanged.
        // Field names/semantics deliberately mirror Coupon.DiscountType/DiscountValue/
        // IsActive so no new discount convention is introduced.
        public int? ProductID { get; set; }

        [MaxLength(20)]
        public string? DiscountType { get; set; }

        public decimal? DiscountValue { get; set; }

        // Null = no end date (sale runs until IsActive is turned off).
        public DateTime? SaleEndDate { get; set; }

        // Mirrors Coupon.IsActive. Defaults true so promotions created before
        // this feature existed (ProductID == null) are unaffected either way.
        public bool IsActive { get; set; } = true;

        public Product? Product { get; set; }

        public int RecipientCount { get; set; }

        public bool IsSent { get; set; } = true;

        [MaxLength(50)]
        public string Status { get; set; } = "Sent";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; } = DateTime.UtcNow;

        public Store? Store { get; set; }

        public ICollection<PromotionRecipient> PromotionRecipients { get; set; } = new List<PromotionRecipient>();
    }
}