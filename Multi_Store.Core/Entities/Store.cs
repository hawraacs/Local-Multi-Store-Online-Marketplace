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

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedBy { get; set; }

        // ===========================
        // Subscription Fields
        // ===========================

        public string SubscriptionStatus { get; set; } = "Pending";
        // Active, Expired, Suspended

        public DateTime? SubscriptionExpiryDate { get; set; }
        // null = never expires (admin override)

        public DateTime? TrialStartDate { get; set; }
        // start of free trial

        public DateTime? LastPaymentDate { get; set; }

        public decimal? LastPaymentAmount { get; set; }
        public decimal OutstandingBalance { get; set; } = 0;

        public DateTime? GracePeriodEndDate { get; set; }

        public bool IsSuspended { get; set; } = false;

        // Navigation Properties

        public virtual User Owner { get; set; } = null!;

        public virtual User? Approver { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();

        public virtual ICollection<DeliveryArea> DeliveryAreas { get; set; } = new List<DeliveryArea>();

        public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

        public virtual ICollection<StoreFollow> Followers { get; set; }
            = new List<StoreFollow>();
        public virtual ICollection<ExplorePost> ExplorePosts { get; set; }
    = new List<ExplorePost>();
    }
}