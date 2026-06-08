using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class DeliveryPerson
    {
        public int DeliveryPersonID { get; set; }

        public int UserID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;

        public string VehicleNumber { get; set; } = string.Empty;

        public string DrivingLicenseNumber { get; set; } = string.Empty;

        public string? IDProofURL { get; set; }

        public string? RejectionReason { get; set; }

        public decimal? CurrentLatitude { get; set; }

        public decimal? CurrentLongitude { get; set; }

        public DateTime? LastLocationUpdate { get; set; }

        public string Status { get; set; } = "Pending";

        public decimal Rating { get; set; } = 0;

        public bool IsActive { get; set; } = false;

        public DateTime? ApprovedAt { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual ICollection<DeliveryAssignment> Assignments { get; set; } = new List<DeliveryAssignment>();
    }
}