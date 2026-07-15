using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class StoryLikeRepository : Repository<StoryLike>, IStoryLikeRepository
    {
        private readonly ApplicationDbContext _context;

        public StoryLikeRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> IsLikedByCustomerAsync(int storyId, int customerId)
        {
            return await _context.StoryLikes
                .AnyAsync(l => l.StoryID == storyId && l.CustomerID == customerId);
        }

        public async Task<int> GetLikeCountAsync(int storyId)
        {
            return await _context.StoryLikes
                .CountAsync(l => l.StoryID == storyId);
        }

        public async Task<List<StoryLike>> GetLikesForStoryAsync(int storyId)
        {
            return await _context.StoryLikes
                .Include(l => l.Customer)
                    .ThenInclude(c => c.User)
                .Where(l => l.StoryID == storyId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task LikeAsync(int storyId, int customerId)
        {
            var exists = await _context.StoryLikes
                .AnyAsync(l => l.StoryID == storyId && l.CustomerID == customerId);

            if (exists) return;

            _context.StoryLikes.Add(new StoryLike
            {
                StoryID = storyId,
                CustomerID = customerId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public async Task UnlikeAsync(int storyId, int customerId)
        {
            var like = await _context.StoryLikes
                .FirstOrDefaultAsync(l => l.StoryID == storyId && l.CustomerID == customerId);

            if (like == null) return;

            _context.StoryLikes.Remove(like);
            await _context.SaveChangesAsync();
        }
    }
}
