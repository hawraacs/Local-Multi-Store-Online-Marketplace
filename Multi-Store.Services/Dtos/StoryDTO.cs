namespace Multi_Store.Services.Dtos
{
    public class StoryDTO
    {
        public int StoryID { get; set; }
        public int StoreID { get; set; }
        public string MediaType { get; set; } = "Image";
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public int? DurationSeconds { get; set; }
        public string? Caption { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsViewed { get; set; }

        // NEW - shown under each thumbnail on the Store Owner's "My Stories" card,
        // and used by the customer viewer's like button state.
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int ReplyCount { get; set; }
        public bool IsLikedByCurrentCustomer { get; set; }
    }

    // One circle in the story bar = one Store + all of its active stories,
    // already in the order they should play (oldest first).
    public class StoryGroupDTO
    {
        public int StoreID { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? StoreLogoUrl { get; set; }
        public bool HasUnviewedStories { get; set; }
        public List<StoryDTO> Stories { get; set; } = new();
    }
}