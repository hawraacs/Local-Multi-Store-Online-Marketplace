using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class BlockRelation
    {
        public int Id { get; set; }

        public int BlockerUserId { get; set; }
        public int BlockedUserId { get; set; }

        public string BlockerRole { get; set; } = null!;
        public string BlockedRole { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
