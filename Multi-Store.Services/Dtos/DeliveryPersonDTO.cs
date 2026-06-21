using Multi_Store.Core.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Services.Dtos
{
    public class DeliveryPersonDTO
    {
        public int DeliveryPersonID { get; set; }

        public int UserID { get; set; }
        public int? RequestedByUserID { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Area is required.")]
        public string Area { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle type is required.")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vehicle number is required.")]
        public string VehicleNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Driving license number is required.")]
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

        public User? User { get; set; }
    }
}