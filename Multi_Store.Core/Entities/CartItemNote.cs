using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class CartItemNote
    {
        public int CartItemNoteID { get; set; }

        // Plain int, not a navigation property — no FK constraint,
        // so this table has zero coupling to CartItem's schema.
        public int CartItemID { get; set; }

        public string Note { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
    }

}
