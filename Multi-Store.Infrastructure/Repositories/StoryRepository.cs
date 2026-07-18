using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class StoryRepository : Repository<Story>, IStoryRepository
    {
        private readonly ApplicationDbContext _context;

        public StoryRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        // =====================
        // STORE OWNER'S OWN STORIES
        // =====================

        public async Task<List<Story>> GetActiveStoriesByStoreAsync(int storeId)
        {
            var now = DateTime.UtcNow;

            return await _context.Stories
                .Where(s => s.StoreID == storeId && s.IsActive && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt) // oldest -> newest: correct Instagram playback order within a store
                .ToListAsync();
        }

        // =====================
        // FEED / FOLLOWED STORES
        // =====================

        public async Task<List<Story>> GetActiveStoriesForFollowedStoresAsync(int customerId)
        {
            var now = DateTime.UtcNow;

            return await _context.Stories
                .Include(s => s.Store)
                .Where(s =>
                    s.IsActive &&
                    s.ExpiresAt > now &&
                    _context.StoreFollows.Any(f =>
                        f.CustomerID == customerId &&
                        f.StoreID == s.StoreID))
                .OrderBy(s => s.CreatedAt) // oldest -> newest per story; the page model groups by store
                .ToListAsync();
        }

        public async Task<List<Story>> GetActiveStoriesForStoreAsync(int storeId)
        {
            var now = DateTime.UtcNow;

            return await _context.Stories
                .Include(s => s.Store)
                .Where(s => s.StoreID == storeId && s.IsActive && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt) // oldest -> newest: correct Instagram playback order within a store
                .ToListAsync();
        }

        public async Task<Story?> GetByIdWithStoreAsync(int storyId)
        {
            return await _context.Stories
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.StoryID == storyId);
        }

        public async Task<Story?> GetStoryForOwnerAsync(int storyId, int storeOwnerUserId)
        {
            var story = await _context.Stories
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.StoryID == storyId);

            if (story == null) return null;
            if (story.Store.OwnerUserID != storeOwnerUserId) return null;

            return story;
        }

        // =====================
        // OWNERSHIP-CHECKED SOFT DELETE
        // =====================

        public async Task DeactivateStoryAsync(int storyId, int storeOwnerId)
        {
            var story = await _context.Stories
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.StoryID == storyId);

            if (story == null)
                return;

            if (story.Store.OwnerUserID != storeOwnerId)
                return;

            story.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
