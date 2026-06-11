using System;

namespace Multi_Store.Core.Entities
{
    public class PromotionRecipient
    {
        public int PromotionRecipientID { get; set; }

        public int PromotionID { get; set; }

        public int CustomerID { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Promotion? Promotion { get; set; }

        public Customer? Customer { get; set; }
    }
}