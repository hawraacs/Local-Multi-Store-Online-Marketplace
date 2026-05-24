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

        public bool IsVerified { get; set; }

        public int LoyaltyPoints { get; set; }

        public bool CODBlocked { get; set; }

        // Navigation Properties
        public User? User { get; set; }

        public ICollection<CustomerAddress>? Addresses { get; set; }

        public CustomerAddress? DefaultAddress { get; set; }

        public ICollection<Cart>? Carts { get; set; }
    }
}