using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Entities;
    using Multi_Store.Core.Reposinterface;
    using Multi_Store.Infrastructure.Data;
    using Multi_Store.Infrastructure.Repositories.Base;

    namespace Multi_Store.Infrastructure.Repositories
    {
        public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
        {
            private readonly ApplicationDbContext _context;

            public AuditLogRepository(ApplicationDbContext context) : base(context)
            {
                _context = context;
            }

            public async Task<IReadOnlyList<AuditLog>> GetLogsByUserAsync(int userId)
            {
                return await _context.AuditLogs
                    .Where(log => log.UserID == userId)
                    .ToListAsync();
            }

            public async Task<IReadOnlyList<AuditLog>> GetLogsByActionAsync(string action)
            {
                return await _context.AuditLogs
                    .Where(log => log.Action == action)
                    .ToListAsync();
            }
        }
    }
