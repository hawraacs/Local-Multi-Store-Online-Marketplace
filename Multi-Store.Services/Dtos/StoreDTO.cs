using Multi_Store.Core.Entities;
using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class StoreDTO
    {
        public int StoreID { get; set; }

        public int OwnerUserID { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string StoreCode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? LogoURL { get; set; }

        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;

        public string? AddressLine2 { get; set; }

        public string City { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        // Used only for nearby stores display. Not a database column.
        public double DistanceKm { get; set; }

        public string? BusinessLicenseNumber { get; set; }

        public string? BusinessLicenseURL { get; set; }

        public decimal Rating { get; set; } = 0;

        public int TotalRatings { get; set; } = 0;

        public string Status { get; set; } = "Pending";

        public decimal CommissionRate { get; set; } = 10.0m;

        public bool CODSupported { get; set; } = true;

        public decimal CODMaxLimit { get; set; } = 5000;

        // UC-31: Fixed delivery fee option
        public bool HasFixedDeliveryFee { get; set; } = false;

        public decimal? FixedDeliveryFee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFollowing { get; set; } = false;

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedBy { get; set; }

        // =========================
        // NAVIGATION (existing)
        // =========================
        public virtual User Owner { get; set; } = null!;

        public virtual User? Approver { get; set; }

        public List<ProductDTO> Products { get; set; } = new();


        public virtual ICollection<DeliveryArea> DeliveryAreas { get; set; } = new List<DeliveryArea>();

        public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public List<ReviewDTO> Reviews { get; set; } = new();

        public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

        // =========================
        // NEW: SUBSCRIPTION SYSTEM (FIX)
        // =========================

        public string SubscriptionStatus { get; set; } = "Active";
        // Active, Expired, Suspended

        public DateTime? SubscriptionExpiryDate { get; set; }

        public string? OwnerEmail { get; set; }
    }
}