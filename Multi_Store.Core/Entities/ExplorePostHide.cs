using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class ExplorePostHide
    {
        public int ExplorePostHideID { get; set; }
        public int CustomerId { get; set; }
        public int ExplorePostId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
