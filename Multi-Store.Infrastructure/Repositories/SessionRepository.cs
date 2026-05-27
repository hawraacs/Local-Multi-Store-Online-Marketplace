using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Infrastructure.Repositories
{
    public class SessionRepository : Repository<Session>, ISessionRepository
    {
        private readonly ApplicationDbContext _context;

        public SessionRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Session?> GetByTokenAsync(string token)
        {
            return await _context.Sessions
                .FirstOrDefaultAsync(s => s.SessionToken == token);
        }

        public async Task<IReadOnlyList<Session>> GetByUserAsync(int userId)
        {
            return await _context.Sessions
                .Where(s => s.UserID == userId)
                .OrderByDescending(s => s.LastActivityAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Session>> GetActiveSessionsAsync(int userId)
        {
            var now = DateTime.UtcNow;

            return await _context.Sessions
                .Where(s =>
                    s.UserID == userId &&
                    s.IsActive &&
                    s.ExpiresAt > now)
                .OrderByDescending(s => s.LastActivityAt)
                .ToListAsync();
        }

        public async Task RemoveExpiredSessionsAsync()
        {
            var now = DateTime.UtcNow;

            var expiredSessions = await _context.Sessions
                .Where(s => s.ExpiresAt <= now || !s.IsActive)
                .ToListAsync();

            if (expiredSessions.Any())
            {
                _context.Sessions.RemoveRange(expiredSessions);
                await _context.SaveChangesAsync();
            }
        }
    }
}
