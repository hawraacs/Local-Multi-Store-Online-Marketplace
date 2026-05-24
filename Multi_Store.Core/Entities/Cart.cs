using System;

namespace Multi_Store.Core.Entities
{
    public class Cart
    {
        public int CartID { get; set; }

        public int? CustomerID { get; set; }

        public string? SessionToken { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        // Navigation Properties
        public Customer? Customer { get; set; }
    }
}