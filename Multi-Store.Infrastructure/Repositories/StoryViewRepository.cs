using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class StoryViewRepository : Repository<StoryView>, IStoryViewRepository
    {
        private readonly ApplicationDbContext _context;

        public StoryViewRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<int>> GetViewedStoryIdsAsync(int customerId)
        {
            return await _context.StoryViews
                .Where(v => v.CustomerID == customerId)
                .Select(v => v.StoryID)
                .ToListAsync();
        }

        public async Task MarkViewedAsync(int storyId, int customerId)
        {
            var exists = await _context.StoryViews
                .AnyAsync(v => v.StoryID == storyId && v.CustomerID == customerId);

            if (exists) return;

            _context.StoryViews.Add(new StoryView
            {
                StoryID = storyId,
                CustomerID = customerId,
                ViewedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public async Task<List<StoryView>> GetViewsForStoryAsync(int storyId)
        {
            return await _context.StoryViews
                .Include(v => v.Customer)
                    .ThenInclude(c => c.User)
                .Where(v => v.StoryID == storyId)
                .OrderByDescending(v => v.ViewedAt)
                .ToListAsync();
        }
    }
}
