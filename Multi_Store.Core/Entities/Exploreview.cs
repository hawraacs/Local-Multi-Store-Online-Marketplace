namespace Multi_Store.Core.Entities
{
    public class ExploreView
    {
        public int ExploreViewID { get; set; }

        public int ExplorePostID { get; set; }

        public int CustomerID { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        public virtual ExplorePost ExplorePost { get; set; } = null!;

        public virtual Customer Customer { get; set; } = null!;
    }
}