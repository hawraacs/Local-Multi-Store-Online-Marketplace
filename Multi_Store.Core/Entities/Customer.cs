using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class Customer
    {
        public int CustomerID { get; set; }
        public int UserID { get; set; }
        public int? DefaultAddressID { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public bool IsVerified { get; set; } = false;
        public int LoyaltyPoints { get; set; } = 0;
        public bool CODBlocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual CustomerAddress? DefaultAddress { get; set; }

        public virtual ICollection<CustomerAddress> Addresses { get; set; }
            = new List<CustomerAddress>();

        // Wishlist
        public virtual ICollection<Wishlist> Wishlists { get; set; }
            = new List<Wishlist>();

        // Orders
        public virtual ICollection<Order> Orders { get; set; }
            = new List<Order>();

        // Reviews
        public virtual ICollection<Review> Reviews { get; set; }
            = new List<Review>();

        // Complaints
        public virtual ICollection<Complaint> Complaints { get; set; }
            = new List<Complaint>();

        // Carts
        public virtual ICollection<Cart> Carts { get; set; }
            = new List<Cart>();

        // Recently Viewed Products
        public virtual ICollection<RecentlyViewedProduct> RecentlyViewedProducts { get; set; }
            = new List<RecentlyViewedProduct>();

        public virtual ICollection<StoreFollow> FollowedStores { get; set; }
    = new List<StoreFollow>();
        public virtual ICollection<ExploreLike> ExploreLikes { get; set; }
    = new List<ExploreLike>();

        public virtual ICollection<ExploreComment> ExploreComments { get; set; }
            = new List<ExploreComment>();
    }
}