using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class Store
    {
        public int StoreID { get; set; }

        public int OwnerUserID { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string StoreCode { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? LogoURL { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;

        public string? AddressLine2 { get; set; }

        public string City { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? BusinessLicenseNumber { get; set; }

        public string? BusinessLicenseURL { get; set; }

        public double Rating { get; set; }

        public int TotalRatings { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal CommissionRate { get; set; }

        public bool CODSupported { get; set; }

        public decimal? CODMaxLimit { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedBy { get; set; }

        // Navigation Properties
        public User? OwnerUser { get; set; }

        public ICollection<DeliveryArea>? DeliveryAreas { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}