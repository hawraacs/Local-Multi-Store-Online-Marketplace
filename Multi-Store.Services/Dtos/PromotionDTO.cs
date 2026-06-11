using System;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Core.DTOs
{
    public class PromotionDTO
    {
        public int PromotionID { get; set; }

        public int StoreID { get; set; }

        public int CreatedByUserID { get; set; }

        [Required(ErrorMessage = "Promotion title is required")]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Promotion message is required")]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public string AudienceType { get; set; } = "AllCustomers";

        [StringLength(50)]
        public string? CouponCode { get; set; }

        public int RecipientCount { get; set; }

        public bool IsSent { get; set; }

        public string Status { get; set; } = "Sent";

        public DateTime CreatedAt { get; set; }

        public DateTime? SentAt { get; set; }

        // Coupon creation fields
        public bool CreateCoupon { get; set; } = false;

        public string DiscountType { get; set; } = "Percentage";

        [Range(0, 999999)]
        public decimal DiscountValue { get; set; }

        [Range(0, 999999)]
        public decimal? MinimumOrderAmount { get; set; }

        [Range(0, 999999)]
        public decimal? MaximumDiscountAmount { get; set; }

        public DateTime? CouponEndDate { get; set; }

        public int? UsageLimit { get; set; } = 100;

        public int? UsagePerCustomerLimit { get; set; } = 1;
    }
}