// Entities/CartItem.cs
using Multi_Store.Core.Entities;
using System;

namespace Multi_Store.Services.Dtos
{
    public class CartItemDTO
    {
        public int CartItemID { get; set; }
        public int CartID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtAddTime { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // ⚠️ THESE NAVIGATION PROPERTIES ARE REQUIRED
        public virtual Cart Cart { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}