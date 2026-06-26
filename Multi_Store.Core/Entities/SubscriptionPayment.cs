using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
        public class SubscriptionPayment
        {
            public int Id { get; set; }

            public int StoreId { get; set; }
            public Store Store { get; set; }

            public decimal Amount { get; set; }

            public DateTime PaymentDate { get; set; }

            public string? Reference { get; set; }

            public string? PaymentMethod { get; set; }
        }
    }