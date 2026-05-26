using System;

namespace Multi_Store.Services.Dtos
{
    public class RefundRequestDTO
    {
        // Primary Key
        public int RefundRequestID { get; set; }

        // Foreign Keys
        public int OrderID { get; set; }

        public int CustomerID { get; set; }

        // Attributes
        public string Reason { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? EvidenceImageURL { get; set; }

        public decimal RequestedAmount { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal? ApprovedAmount { get; set; }

        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        // Relationships

        // Many RefundRequests belong to one Order
        public Order? Order { get; set; }

        // Many RefundRequests belong to one Customer
        public Customer? Customer { get; set; }
    }
}