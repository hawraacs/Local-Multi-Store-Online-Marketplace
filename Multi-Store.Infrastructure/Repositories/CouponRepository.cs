using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

namespace Multi_Store.Infrastructure.Repositories
{
    public class CouponRepository
        : Repository<Coupon>, ICouponRepository
    {
        private readonly ApplicationDbContext _context;

        public CouponRepository(
            ApplicationDbContext context)
            : base(context)
        {
            _context = context;
        }

        // =====================================================
        // GET COUPON BY CODE
        // =====================================================
        public async Task<Coupon?> GetByCodeAsync(
            string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return null;
            }

            var cleanCode =
                couponCode
                    .Trim()
                    .ToUpper();

            return await _context.Coupons
                .FirstOrDefaultAsync(c =>
                    c.CouponCode.ToUpper() ==
                    cleanCode);
        }

        // =====================================================
        // GET ACTIVE COUPONS
        // =====================================================
        public async Task<IReadOnlyList<Coupon>>
            GetActiveCouponsAsync()
        {
            return await _context.Coupons
                .AsNoTracking()
                .Where(c =>
                    c.IsActive)
                .OrderBy(c =>
                    c.EndDate)
                .ToListAsync();
        }

        // =====================================================
        // GET COUPONS BY STORE
        // =====================================================
        public async Task<IReadOnlyList<Coupon>>
            GetByStoreAsync(
                int storeId)
        {
            return await _context.Coupons
                .AsNoTracking()
                .Where(c =>
                    c.StoreID == storeId)
                .OrderByDescending(c =>
                    c.StartDate)
                .ToListAsync();
        }

        // =====================================================
        // GET CURRENTLY VALID COUPONS
        // =====================================================
        public async Task<IReadOnlyList<Coupon>>
            GetValidCouponsAsync(
                DateTime currentDate)
        {
            var date =
                currentDate.Date;

            return await _context.Coupons
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&

                    c.StartDate.Date <= date &&

                    c.EndDate.Date >= date &&

                    (
                        !c.UsageLimit.HasValue ||
                        c.UsedCount <
                        c.UsageLimit.Value
                    ))
                .OrderBy(c =>
                    c.EndDate)
                .ToListAsync();
        }
    }
}

