using System;

namespace Multi_Store.Services.Dtos
{
    public class Complaint
    {
        // Primary Key
        public int ComplaintID { get; set; }

        // Foreign Keys
        public int CustomerID { get; set; }

        public int? OrderID { get; set; }

        public int? StoreID { get; set; }

        public int? ProductID { get; set; }

        // Attributes
        public string ComplaintType { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? EvidenceImageURL { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Resolution { get; set; }

        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        // Relationships

        // Many Complaints belong to one Customer
        public Customer? Customer { get; set; }

        // Many Complaints can reference one Order
        public Order? Order { get; set; }

        // Many Complaints can reference one Store
        public Store? Store { get; set; }

        // Many Complaints can reference one Product
        public Product? Product { get; set; }
    }
}