using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    // A customer's review of a delivery partner's service on a specific
    // order. Deliberately independent from Review (which models
    // product/store feedback) — different domain, different lifecycle
    // (no moderation Status here: a delivery review is approved the
    // moment it's submitted, since it drives an immediate rating
    // recalculation rather than a store-owner-facing moderation queue).
    public class DeliveryReview
    {
        public int DeliveryReviewID { get; set; }

        public int OrderID { get; set; }
        public Order Order { get; set; }

        public int CustomerID { get; set; }
        public Customer Customer { get; set; }

        public int DeliveryPersonID { get; set; }
        public DeliveryPerson DeliveryPerson { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

