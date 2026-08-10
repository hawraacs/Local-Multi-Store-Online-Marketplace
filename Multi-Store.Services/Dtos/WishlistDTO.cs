// Entities/Wishlist.cs
using Multi_Store.Core.Entities;
using System;
namespace Multi_Store.Services.Dtos
{
    public class WishlistDTO
    {
        public int WishlistID { get; set; }
        public int CustomerID { get; set; }
        public int ProductID { get; set; }
        public DateTime AddedAt { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = "/images/no-image.png";
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public bool IsOutOfStock { get; set; }

        // Added for the Wishlist redesign — real category name from
        // Product.Category, populated in WishlistManager below.
        public string CategoryName { get; set; } = string.Empty;
    }
}
