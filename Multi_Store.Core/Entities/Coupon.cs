using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class Coupon
    {
        // Primary Key
        private int couponID;

        // Foreign Key (Nullable)
        private int? storeID;

        // Attributes
        private string couponCode;
        private string discountType;
        private decimal discountValue;
        private decimal minimumOrderAmount;
        private decimal maximumDiscountAmount;

        private DateTime startDate;
        private DateTime endDate;

        private int usageLimit;
        private int usagePerCustomerLimit;
        private int usedCount;

        private bool isActive;

        // Relationships

        // Many coupons can belong to one store
        private Store store;
    }
}
