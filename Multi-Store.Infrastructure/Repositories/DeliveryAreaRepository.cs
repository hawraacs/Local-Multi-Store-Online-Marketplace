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
    public class DeliveryAreaRepository : Repository<DeliveryArea>, IDeliveryAreaRepository
    {
        private readonly ApplicationDbContext _context;

        public DeliveryAreaRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DeliveryArea>> GetByStoreAsync(int storeId)
        {
            return await _context.DeliveryAreas
                .Where(d => d.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DeliveryArea>> GetActiveAreasAsync(int storeId)
        {
            return await _context.DeliveryAreas
                .Where(d => d.StoreID == storeId && d.IsActive)
                .ToListAsync();
        }

        public async Task<DeliveryArea?> GetByAreaNameAsync(int storeId, string areaName)
        {
            return await _context.DeliveryAreas
                .FirstOrDefaultAsync(d => d.StoreID == storeId && d.AreaName == areaName);
        }

        public async Task<IReadOnlyList<DeliveryArea>> GetFreeDeliveryAreasAsync(int storeId)
        {
            return await _context.DeliveryAreas
                .Where(d => d.StoreID == storeId && d.BaseDeliveryFee == 0)
                .ToListAsync();
        }
    }
}
