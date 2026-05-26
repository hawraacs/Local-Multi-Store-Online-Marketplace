// Entities/DeliveryPerson.cs
using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class DeliveryPersonDTO
    {
        public int DeliveryPersonID { get; set; }
        public int UserID { get; set; }
        public string VehicleType { get; set; } = string.Empty;
        public string VehicleNumber { get; set; } = string.Empty;
        public string DrivingLicenseNumber { get; set; } = string.Empty;
        public string? IDProofURL { get; set; }
        public decimal? CurrentLatitude { get; set; }
        public decimal? CurrentLongitude { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
        public string Status { get; set; } = "Available";
        public decimal Rating { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<DeliveryAssignment> Assignments { get; set; } = new List<DeliveryAssignment>();
    }
}