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