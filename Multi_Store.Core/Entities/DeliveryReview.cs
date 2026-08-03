using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class DeliveryReview
    {
        public int DeliveryReviewID { get; set; }

        public int CustomerID { get; set; }
        public Customer Customer { get; set; } = null!;

        public int DeliveryPersonID { get; set; }
        public DeliveryPerson DeliveryPerson { get; set; } = null!;

        // Ties the review to the specific completed delivery being
        // reviewed - the delivery-side equivalent of Review.OrderItemID.
        // Required (not nullable): a delivery review only makes sense in
        // the context of one specific completed delivery assignment.
        public int AssignmentID { get; set; }
        public DeliveryAssignment Assignment { get; set; } = null!;

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
