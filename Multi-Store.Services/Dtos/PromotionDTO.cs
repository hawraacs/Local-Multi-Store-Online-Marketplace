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
    }
}