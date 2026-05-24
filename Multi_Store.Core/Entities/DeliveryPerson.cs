using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class DeliveryPerson
    {
        // Primary Key
        public int DeliveryPersonID { get; set; }

        // Foreign Key
        public int UserID { get; set; }

        // Attributes
        public string VehicleType { get; set; } = string.Empty;

        public string? VehicleNumber { get; set; }

        public string? DrivingLicenseNumber { get; set; }

        public string? IDProofURL { get; set; }

        public double? CurrentLatitude { get; set; }

        public double? CurrentLongitude { get; set; }

        public DateTime? LastLocationUpdate { get; set; }

        public string Status { get; set; } = string.Empty;

        public double Rating { get; set; }

        public bool IsActive { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // Relationships

        // Many DeliveryPersons belong to one User
        public User? User { get; set; }

        // One DeliveryPerson can have many assignments
        public ICollection<DeliveryAssignment>? DeliveryAssignments { get; set; }
    }
}