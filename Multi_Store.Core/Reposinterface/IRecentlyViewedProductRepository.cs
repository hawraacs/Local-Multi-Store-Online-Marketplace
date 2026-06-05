using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IRecentlyViewedProductRepository
        : IRepository<RecentlyViewedProduct>
    {
        Task<IReadOnlyList<RecentlyViewedProduct>> GetByCustomerAsync(int customerId);

        Task<RecentlyViewedProduct?> GetByCustomerAndProductAsync(
            int customerId,
            int productId);

        Task<bool> ExistsAsync(int customerId, int productId);
    }
}