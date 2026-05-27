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
    public class NotificationRepository : Repository<Notification>, INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Notification>> GetByUserAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Notification>> GetByTypeAsync(int userId, string type)
        {
            return await _context.Notifications
                .Where(n => n.UserID == userId && n.Type == type)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserID == userId && !n.IsRead);
        }
    }
}
