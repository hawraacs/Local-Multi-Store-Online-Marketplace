namespace Multi_Store.Core.Entities
{
    public class Review
    {
        public int ReviewID { get; set; }

        public int CustomerID { get; set; }
        public Customer Customer { get; set; }

        // Store is ALWAYS required (fix your FK issue)
        public int StoreID { get; set; }
        public Store Store { get; set; }

        // Product review optional
        public int? ProductID { get; set; }
        public Product? Product { get; set; }

        // OrderItem only for verified purchase reviews
        public int? OrderItemID { get; set; }
        public OrderItem? OrderItem { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public bool IsVerifiedPurchase { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}