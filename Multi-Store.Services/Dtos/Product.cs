using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class Product
    {
        public int ProductID { get; set; }
        public int StoreID { get; set; }
        public int CategoryID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSlug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;
        public decimal? Weight { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsOutOfStock => Quantity <= 0;
        public decimal Rating { get; set; } = 0;
        public int TotalRatings { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Store Store { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;

        // ?? Images property - MAKE SURE THIS EXISTS
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    }
}