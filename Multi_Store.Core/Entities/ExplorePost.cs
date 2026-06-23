namespace Multi_Store.Core.Entities
{
    public class ExplorePost
    {
        public int ExplorePostID { get; set; }

        public int StoreID { get; set; }

        public int? ProductID { get; set; }

        // Image, Carousel, or Reel
        public string PostType { get; set; } = "Image";

        public string Caption { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsFeatured { get; set; }

        public int ViewCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual Store Store { get; set; } = null!;

        public virtual Product? Product { get; set; }

        public virtual ICollection<ExploreMedia> Media { get; set; }
            = new List<ExploreMedia>();

        public virtual ICollection<ExploreLike> Likes { get; set; }
            = new List<ExploreLike>();

        public virtual ICollection<ExploreComment> Comments { get; set; }
            = new List<ExploreComment>();
    }
}
