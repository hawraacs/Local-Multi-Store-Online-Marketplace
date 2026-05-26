using System;

namespace Multi_Store.Services.Dtos
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

        // Relationships

        // Many Assignments belong to one Order
        public Order? Order { get; set; }

        // Many Assignments belong to one DeliveryPerson
        public DeliveryPerson? DeliveryPerson { get; set; }
    }
}