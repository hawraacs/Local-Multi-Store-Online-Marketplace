using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;

namespace Multi_Store.Services.Managers
{
    public class StoryManager
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IStoryViewRepository _storyViewRepository;
        private readonly IStoryLikeRepository _storyLikeRepository;

        public StoryManager(
            IStoryRepository storyRepository,
            IStoryViewRepository storyViewRepository,
            IStoryLikeRepository storyLikeRepository)
        {
            _storyRepository = storyRepository;
            _storyViewRepository = storyViewRepository;
            _storyLikeRepository = storyLikeRepository;
        }

        // =====================
        // READ
        // =====================

        public Task<List<Story>> GetOwnStoriesAsync(int storeId)
            => _storyRepository.GetActiveStoriesByStoreAsync(storeId);

        public Task<List<Story>> GetFollowedStoriesAsync(int customerId)
            => _storyRepository.GetActiveStoriesForFollowedStoresAsync(customerId);

        public Task<List<Story>> GetStoreStoriesAsync(int storeId)
            => _storyRepository.GetActiveStoriesForStoreAsync(storeId);

        public Task<Story?> GetStoryForOwnerAsync(int storyId, int storeOwnerUserId)
            => _storyRepository.GetStoryForOwnerAsync(storyId, storeOwnerUserId);

        public Task<Story?> GetByIdWithStoreAsync(int storyId)
            => _storyRepository.GetByIdWithStoreAsync(storyId);

        // =====================
        // CREATE
        // =====================

        /*
         * The media file itself is validated and saved to disk by the calling page
         * (same convention as StoreProfileModel.SaveStoreLogoAsync) - this method
         * only creates the Story record once a relative Url already exists, and owns
         * the business rule that every Story expires exactly 24 hours after creation.
         */
        public async Task<Story> CreateStoryAsync(
            int storeId,
            string mediaType,
            string? imageUrl,
            string? videoUrl,
            int? durationSeconds,
            string? caption)
        {
            var now = DateTime.UtcNow;

            var story = new Story
            {
                StoreID = storeId,
                MediaType = mediaType,
                ImageUrl = imageUrl,
                VideoUrl = videoUrl,
                DurationSeconds = durationSeconds,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                CreatedAt = now,
                ExpiresAt = now.AddHours(24),
                IsActive = true
            };

            return await _storyRepository.AddAsync(story);
        }

        // =====================
        // SOFT DELETE
        // =====================

        public Task DeactivateStoryAsync(int storyId, int storeOwnerId)
            => _storyRepository.DeactivateStoryAsync(storyId, storeOwnerId);

        // =====================
        // VIEWED STATE (DB-backed)
        // =====================

        public Task<List<int>> GetViewedStoryIdsAsync(int customerId)
            => _storyViewRepository.GetViewedStoryIdsAsync(customerId);

        public Task<List<StoryView>> GetViewsForStoryAsync(int storyId)
            => _storyViewRepository.GetViewsForStoryAsync(storyId);

        public Task MarkStoryViewedAsync(int storyId, int customerId)
            => _storyViewRepository.MarkViewedAsync(storyId, customerId);

        // =====================
        // LIKES (NEW)
        // =====================

        public Task<bool> IsLikedByCustomerAsync(int storyId, int customerId)
            => _storyLikeRepository.IsLikedByCustomerAsync(storyId, customerId);

        public Task<int> GetLikeCountAsync(int storyId)
            => _storyLikeRepository.GetLikeCountAsync(storyId);

        public Task<List<StoryLike>> GetLikesForStoryAsync(int storyId)
            => _storyLikeRepository.GetLikesForStoryAsync(storyId);

        public Task LikeStoryAsync(int storyId, int customerId)
            => _storyLikeRepository.LikeAsync(storyId, customerId);

        public Task UnlikeStoryAsync(int storyId, int customerId)
            => _storyLikeRepository.UnlikeAsync(storyId, customerId);
    }
}
