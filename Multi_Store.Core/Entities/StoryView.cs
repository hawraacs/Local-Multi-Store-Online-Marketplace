using System;

namespace Multi_Store.Core.Entities
{
    public class StoryView
    {
        public int StoryViewID { get; set; }

        public int StoryID { get; set; }

        public int CustomerID { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        public virtual Story Story { get; set; } = null!;

        public virtual Customer Customer { get; set; } = null!;
    }
}
