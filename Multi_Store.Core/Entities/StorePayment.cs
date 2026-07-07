using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class StorePayment
        {
            public int StorePaymentId { get; set; }
            public int StoreId { get; set; }
            public decimal Amount { get; set; }
            public string Description { get; set; } = string.Empty;
            public DateTime DueDate { get; set; }
            public string Status { get; set; } = "Pending"; // Pending, Paid, Failed
            public string? StripeTransferId { get; set; }   // optional reference
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? PaidAt { get; set; }

            public Store Store { get; set; } = null!;
        }
    }