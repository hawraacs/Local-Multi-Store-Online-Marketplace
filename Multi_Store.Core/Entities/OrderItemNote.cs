using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class OrderItemNote
    {
        public int OrderItemNoteID { get; set; }

        public int OrderID { get; set; }

        public int ProductID { get; set; }

        public string Note { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
