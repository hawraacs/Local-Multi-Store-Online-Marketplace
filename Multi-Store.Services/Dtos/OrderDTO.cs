using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class OrderDTO
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerID { get; set; }
        public int AddressID { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Unpaid";
        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? Notes { get; set; }

        // ⚠️ ALL THESE NAVIGATION PROPERTIES ARE REQUIRED
        public virtual Customer Customer { get; set; } = null!;
        public virtual CustomerAddress Address { get; set; } = null!;

        // OrderItems collection
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // StatusHistory collection
        public virtual ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

        // Payments collection
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // RefundRequest (optional one-to-one)
        public virtual RefundRequest? RefundRequest { get; set; }

        // DeliveryAssignment (optional one-to-one)
        public virtual DeliveryAssignment? DeliveryAssignment { get; set; }
    }
}