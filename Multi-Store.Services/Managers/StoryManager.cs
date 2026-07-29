using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class StoryManager
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IStoryViewRepository _storyViewRepository;
        private readonly IStoryLikeRepository _storyLikeRepository;
        private readonly ApplicationDbContext _context; // NEW — like notification write

        public StoryManager(
            IStoryRepository storyRepository,
            IStoryViewRepository storyViewRepository,
            IStoryLikeRepository storyLikeRepository,
            ApplicationDbContext context)
        {
            _storyRepository = storyRepository;
            _storyViewRepository = storyViewRepository;
            _storyLikeRepository = storyLikeRepository;
            _context = context;
        }

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

        public Task DeactivateStoryAsync(int storyId, int storeOwnerId)
            => _storyRepository.DeactivateStoryAsync(storyId, storeOwnerId);

        public Task<List<int>> GetViewedStoryIdsAsync(int customerId)
            => _storyViewRepository.GetViewedStoryIdsAsync(customerId);

        public Task<List<StoryView>> GetViewsForStoryAsync(int storyId)
            => _storyViewRepository.GetViewsForStoryAsync(storyId);

        public Task MarkStoryViewedAsync(int storyId, int customerId)
            => _storyViewRepository.MarkViewedAsync(storyId, customerId);

        public Task<bool> IsLikedByCustomerAsync(int storyId, int customerId)
            => _storyLikeRepository.IsLikedByCustomerAsync(storyId, customerId);

        public Task<int> GetLikeCountAsync(int storyId)
            => _storyLikeRepository.GetLikeCountAsync(storyId);

        public Task<List<StoryLike>> GetLikesForStoryAsync(int storyId)
            => _storyLikeRepository.GetLikesForStoryAsync(storyId);

        public async Task LikeStoryAsync(int storyId, int customerId)
        {
            await _storyLikeRepository.LikeAsync(storyId, customerId);

            // NEW — notify the store owner about the like.
            var story = await _storyRepository.GetByIdWithStoreAsync(storyId);
            if (story?.Store != null)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = story.Store.OwnerUserID,
                    Title = "Someone liked your story",
                    Message = "A customer liked your story.",
                    Type = "StoryLike",
                    ReferenceID = storyId,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });

                await _context.SaveChangesAsync();
            }
        }

        public Task UnlikeStoryAsync(int storyId, int customerId)
            => _storyLikeRepository.UnlikeAsync(storyId, customerId);
    }
}