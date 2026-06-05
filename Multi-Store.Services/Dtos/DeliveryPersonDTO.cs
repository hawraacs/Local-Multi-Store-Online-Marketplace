// Entities/DeliveryPerson.cs
using Multi_Store.Core.Entities;
using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class DeliveryPersonDTO
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
        public decimal? CurrentLatitude { get; set; }
        public decimal? CurrentLongitude { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal Rating { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime? ApprovedAt { get; set; }

        // Navigation properties
    }
    
}