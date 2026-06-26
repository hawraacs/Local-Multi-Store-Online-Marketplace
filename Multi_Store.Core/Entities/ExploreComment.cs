namespace Multi_Store.Core.Entities
{
    public class ExploreComment
    {
        public int ExploreCommentID { get; set; }

        public int ExplorePostID { get; set; }

        public int CustomerID { get; set; }

        public string CommentText { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual ExplorePost ExplorePost { get; set; } = null!;

        public virtual Customer Customer { get; set; } = null!;
    }
}
