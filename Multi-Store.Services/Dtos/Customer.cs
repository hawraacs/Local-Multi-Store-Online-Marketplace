// Entities/Customer.cs
using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
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

        // Navigation properties - MAKE SURE ALL EXIST
        public virtual User User { get; set; } = null!;
        public virtual CustomerAddress? DefaultAddress { get; set; }
        public virtual ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();

        // ⚠️ Wishlist property
        public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();

        // ⚠️ Orders property
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        // ⚠️ Reviews property
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        // ⚠️ Complaints property
        public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

        public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
    }
}