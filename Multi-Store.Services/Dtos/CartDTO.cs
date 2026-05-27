using Multi_Store.Core.Entities;
using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class CartDTO
    {
        public int CartID { get; set; }
        public int? CustomerID { get; set; }
        public string? SessionToken { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

        // Navigation properties
        public virtual Customer? Customer { get; set; }

        // ?? THIS IS REQUIRED - Collection of CartItems
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}