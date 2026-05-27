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
    public class SystemConfigRepository : Repository<SystemConfig>, ISystemConfigRepository
    {
        private readonly ApplicationDbContext _context;

        public SystemConfigRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SystemConfig?> GetByKeyAsync(string key)
        {
            return await _context.SystemConfigs
                .FirstOrDefaultAsync(c => c.ConfigKey == key);
        }

        public async Task<IReadOnlyList<SystemConfig>> GetByValueTypeAsync(string valueType)
        {
            return await _context.SystemConfigs
                .Where(c => c.ValueType == valueType)
                .ToListAsync();
        }
    }
}
