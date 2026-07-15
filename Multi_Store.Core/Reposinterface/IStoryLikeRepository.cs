using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IStoryLikeRepository : IRepository<StoryLike>
    {
        Task<bool> IsLikedByCustomerAsync(int storyId, int customerId);

        Task<int> GetLikeCountAsync(int storyId);

        // Full rows with Customer->User included, for the Insights "Likes" list
        Task<List<StoryLike>> GetLikesForStoryAsync(int storyId);

        // Idempotent: does nothing if already liked
        Task LikeAsync(int storyId, int customerId);

        // Idempotent: does nothing if not currently liked
        Task UnlikeAsync(int storyId, int customerId);
    }
}
