namespace Multi_Store.Core.Entities
{
    public class ExploreLike
    {
        public int ExploreLikeID { get; set; }

        public int ExplorePostID { get; set; }

        public int CustomerID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ExplorePost ExplorePost { get; set; } = null!;

        public virtual Customer Customer { get; set; } = null!;
    }
}
