using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IStoryRepository : IRepository<Story>
    {
        // =====================
        // STORE OWNER'S OWN STORIES
        // =====================

        // Used by Store Owner Home to show their own story bar (active, non-expired only).
        Task<List<Story>> GetActiveStoriesByStoreAsync(int storeId);

        // =====================
        // FEED / FOLLOWED STORES
        // =====================

        // Used by Customer Feed and Store Customer Profile. Joins against StoreFollows
        // the same way IStoreRepository.GetFeedProductsAsync already does, so only
        // stories from stores this customer follows are returned. Active + non-expired
        // only, newest first.
        Task<List<Story>> GetActiveStoriesForFollowedStoresAsync(int customerId);

        // Used by Store Customer Profile when viewing a single store's page directly
        // (that store's own active stories, regardless of follow status - matches how
        // GetStoreProductsAsync already works for products on that same page).
        Task<List<Story>> GetActiveStoriesForStoreAsync(int storeId);

        // Used by customer-side like/reply handlers - any active story's Store.OwnerUserID
        // is needed to know who to like-notify or route a reply message to. No ownership
        // restriction here (unlike GetStoryForOwnerAsync below, which is for Insights).
        Task<Story?> GetByIdWithStoreAsync(int storyId);

        // Used by the Insights panel to fetch one specific story, but only if the
        // requesting user actually owns the store it belongs to - prevents a Store
        // Owner from viewing another store's Insights by guessing a storyId.
        Task<Story?> GetStoryForOwnerAsync(int storyId, int storeOwnerUserId);

        // =====================
        // OWNERSHIP-CHECKED SOFT DELETE
        // =====================

        // Soft-hides a story (IsActive = false). Never a hard delete, per business rules.
        // Ownership-checked the same way DeleteProductReviewAsync checks storeOwnerId,
        // so a store owner can only hide their own stories.
        Task DeactivateStoryAsync(int storyId, int storeOwnerId);
    }
}
