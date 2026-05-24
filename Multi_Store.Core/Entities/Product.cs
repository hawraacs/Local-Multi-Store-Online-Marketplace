using System;
using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class Product
    {
        public int ProductID { get; set; }

        public int StoreID { get; set; }

        public int CategoryID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string ProductSlug { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }

        public int Quantity { get; set; }

        public int LowStockThreshold { get; set; }

        public double? Weight { get; set; }

        public bool IsActive { get; set; }

        public bool IsOutOfStock { get; set; }

        public double Rating { get; set; }

        public int TotalRatings { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public Store? Store { get; set; }

        public Category? Category { get; set; }

        public ICollection<ProductImage>? ProductImages { get; set; }
    }
}