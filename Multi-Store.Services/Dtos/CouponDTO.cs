// Entities/Coupon.cs
using Multi_Store.Core.Entities;
using System;

namespace Multi_Store.Services.Dtos
{
    public class CouponDTO
    {
        public int CouponID { get; set; }
        public int? StoreID { get; set; }

        // ⚠️ MAKE SURE THIS EXISTS - CouponCode
        public string CouponCode { get; set; } = string.Empty;

        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal? MinimumOrderAmount { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsagePerCustomerLimit { get; set; }
        public int UsedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // ⚠️ MAKE SURE THIS EXISTS - Store navigation property
        public virtual Store? Store { get; set; }
    }
}