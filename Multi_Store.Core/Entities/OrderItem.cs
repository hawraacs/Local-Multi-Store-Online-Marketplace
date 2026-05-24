using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
  
        public class OrderItem
        {
            // Primary Key
            public int OrderItemID { get; set; }

            // Foreign Keys
            public int OrderID { get; set; }

            public int ProductID { get; set; }

            public int StoreID { get; set; }

            // Attributes
            public string ProductName { get; set; } = string.Empty;

            public decimal ProductPrice { get; set; }

            public int Quantity { get; set; }

            public decimal TotalPrice { get; set; }

            public bool ReviewSubmitted { get; set; }

            // Relationships

            // Many OrderItems belong to one Order
            public Order? Order { get; set; }

            // Many OrderItems reference one Product
            public Product? Product { get; set; }

            // Many OrderItems belong to one Store
            public Store? Store { get; set; }
        }
    
}
