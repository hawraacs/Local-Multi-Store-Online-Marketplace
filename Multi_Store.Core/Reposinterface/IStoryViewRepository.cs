using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IStoryViewRepository : IRepository<StoryView>
    {
        // All StoryIDs this customer has already viewed - used to decide gradient vs gray ring.
        Task<List<int>> GetViewedStoryIdsAsync(int customerId);

        // Idempotent: does nothing if this customer already viewed this story.
        Task MarkViewedAsync(int storyId, int customerId);

        // Full rows with Customer->User included, for the Insights "Viewers" list.
        // Also doubles as "Total Views"/"Unique Viewers" count (they're always equal,
        // since the unique index on (StoryID, CustomerID) guarantees one row per customer).
        Task<List<StoryView>> GetViewsForStoryAsync(int storyId);
    }
}
