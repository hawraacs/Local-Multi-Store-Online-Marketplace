using System;

namespace Multi_Store.Core.Entities
{
    public class Story
    {
        public int StoryID { get; set; }

        // FK -> Store.StoreID. Only Store Owners can own a Story (enforced at the
        // Manager/Page level) - there is intentionally no CustomerID anywhere on
        // this entity, so a Customer can never be the author of a Story.
        public int StoreID { get; set; }

        // "Image" (default) or "Video". Existing ImageUrl-only stories are unaffected -
        // MediaType simply defaults to "Image" for them.
        public string MediaType { get; set; } = "Image";

        // Populated when MediaType == "Image". Null for video stories.
        public string? ImageUrl { get; set; }

        // Only populated when MediaType == "Video". Null for image stories.
        public string? VideoUrl { get; set; }

        // Actual video duration in seconds, read from the browser when the video
        // is uploaded (via a hidden field set by JS on file selection) so the
        // viewer's progress bar can sync to real playback length instead of guessing.
        public int? DurationSeconds { get; set; }

        public string? Caption { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        // Soft-hide flag. Per the business rules, expired/removed Stories are NEVER
        // hard-deleted - queries always filter IsActive == true AND ExpiresAt > DateTime.UtcNow.
        public bool IsActive { get; set; } = true;

        // Navigation property (one-directional: Store.cs is left untouched,
        // no ICollection<Story> is added back on Store).
        public virtual Store Store { get; set; } = null!;
    }
}
