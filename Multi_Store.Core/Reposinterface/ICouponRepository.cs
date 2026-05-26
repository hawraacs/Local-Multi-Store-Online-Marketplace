using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICouponRepository : IRepository<Coupon>
    {
        Task<Coupon?> GetByCodeAsync(string couponCode);

        Task<IReadOnlyList<Coupon>> GetActiveCouponsAsync();

        Task<IReadOnlyList<Coupon>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<Coupon>> GetValidCouponsAsync(DateTime currentDate);
    }
}
