using System;

namespace Multi_Store.Core.Entities
{
    public class DeliveryAssignment
    {
        // Primary Key
        public int AssignmentID { get; set; }

        // Foreign Keys
        public int OrderID { get; set; }

        public int DeliveryPersonID { get; set; }

        // Attributes
        public DateTime AssignedAt { get; set; }

        public DateTime? PickupTime { get; set; }

        public DateTime? DeliveryTime { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? DeliveryProofImageURL { get; set; }

        // UI-only personal preference for the delivery person.
        // Does NOT delete or affect the order, assignment history,
        // admin visibility, customer visibility, or reporting/analytics.
        public bool IsHiddenByDeliveryPerson { get; set; } = false;

        // Relationships

        // Many Assignments belong to one Order
        public Order? Order { get; set; }

        // Many Assignments belong to one DeliveryPerson
        public DeliveryPerson? DeliveryPerson { get; set; }
    }
}