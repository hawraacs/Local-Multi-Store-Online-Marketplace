using System;

namespace Multi_Store.Services.Dtos
{
    public class ReviewDTO
    {
        // Primary Key
        public int ReviewID { get; set; }

        // Foreign Keys
        public int CustomerID { get; set; }

        public int OrderItemID { get; set; }

        public int StoreID { get; set; }

        public int? ProductID { get; set; }

        // Attributes
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public bool IsVerifiedPurchase { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Relationships

        // Many Reviews belong to one Customer
        public Customer? Customer { get; set; }

        // Many Reviews belong to one OrderItem
        public OrderItem? OrderItem { get; set; }

        // Many Reviews belong to one Store
        public Store? Store { get; set; }

        // Many Reviews can belong to one Product
        public Product? Product { get; set; }
    }
}