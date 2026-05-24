using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
        public class Order
        {
            // Primary Key
            public int OrderID { get; set; }

            // Foreign Keys
            public int CustomerID { get; set; }

            public int AddressID { get; set; }

            // Attributes
            public string OrderNumber { get; set; } = string.Empty;

            public DateTime OrderDate { get; set; }

            public string Status { get; set; } = string.Empty;

            public string PaymentMethod { get; set; } = string.Empty;

            public string PaymentStatus { get; set; } = string.Empty;

            public decimal Subtotal { get; set; }

            public decimal DeliveryFee { get; set; }

            public decimal DiscountAmount { get; set; }

            public decimal TaxAmount { get; set; }

            public decimal TotalAmount { get; set; }

            public string? CancellationReason { get; set; }

            public DateTime? CancelledAt { get; set; }

            public string? Notes { get; set; }

            // Relationships

            // Many Orders belong to one Customer
            public Customer? Customer { get; set; }

            // Many Orders can use one Address
            public CustomerAddress? Address { get; set; }
        }
    
}
