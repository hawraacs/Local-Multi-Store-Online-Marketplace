using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class StoreFollow
    {
        public int StoreFollowID { get; set; }

        public int CustomerID { get; set; }
        public int StoreID { get; set; }

        public DateTime FollowedAt { get; set; }

        public Customer Customer { get; set; } = null!;
        public Store Store { get; set; } = null!;
    }
}
