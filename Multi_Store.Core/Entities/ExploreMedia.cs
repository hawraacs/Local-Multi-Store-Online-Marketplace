namespace Multi_Store.Core.Entities
{
    public class ExploreMedia
    {
        public int ExploreMediaID { get; set; }

        public int ExplorePostID { get; set; }

        // Image or Video
        public string MediaType { get; set; } = "Image";

        public string MediaUrl { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public int DisplayOrder { get; set; }

        public int? DurationSeconds { get; set; }

        public virtual ExplorePost ExplorePost { get; set; } = null!;
    }
}
