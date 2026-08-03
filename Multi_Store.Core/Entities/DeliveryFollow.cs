using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class DeliveryFollow
    {
        public int DeliveryFollowID { get; set; }

        public int CustomerID { get; set; }
        public int DeliveryPersonID { get; set; }

        public DateTime FollowedAt { get; set; }

        public Customer Customer { get; set; } = null!;
        public DeliveryPerson DeliveryPerson { get; set; } = null!;
    }
}