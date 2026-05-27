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
    public class CouponRepository : Repository<Coupon>, ICouponRepository
    {
        private readonly ApplicationDbContext _context;

        public CouponRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Coupon?> GetByCodeAsync(string couponCode)
        {
            return await _context.Coupons
                .FirstOrDefaultAsync(c => c.CouponCode == couponCode);
        }

        public async Task<IReadOnlyList<Coupon>> GetActiveCouponsAsync()
        {
            return await _context.Coupons
                .Where(c => c.IsActive)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Coupon>> GetByStoreAsync(int storeId)
        {
            return await _context.Coupons
                .Where(c => c.StoreID == storeId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Coupon>> GetValidCouponsAsync(DateTime currentDate)
        {
            return await _context.Coupons
                .Where(c => c.IsActive
                            && c.StartDate <= currentDate
                           )
                .ToListAsync();
        }
    }
}
