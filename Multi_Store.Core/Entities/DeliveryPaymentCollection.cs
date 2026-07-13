using System;

namespace Multi_Store.Core.Entities
{
  
    public class DeliveryPaymentCollection
    {
        public int CollectionID { get; set; }

        public int OrderID { get; set; }

        public int DeliveryPersonID { get; set; }

        public decimal CollectedAmount { get; set; }

        public DateTime CollectionDate { get; set; } = DateTime.UtcNow;

        public string? Notes { get; set; }

        // Navigation properties
        public virtual Order Order { get; set; } = null!;

        public virtual DeliveryPerson DeliveryPerson { get; set; } = null!;
    }
}