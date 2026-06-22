using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IStoreRepository : IRepository<Store>
    {
        // =====================
        // BASIC STORE QUERIES
        // =====================

        Task<Store?> GetByCodeAsync(string storeCode);

        Task<Store?> GetByOwnerIdAsync(int ownerUserId);

        Task<Store?> GetStoreDetailsAsync(int storeId);

        Task<List<Store>> SearchStoresAsync(string keyword);

        // =====================
        // STORE FILTERS
        // =====================

        Task<List<Store>> GetByStatusAsync(string status);

        Task<List<Store>> GetApprovedStoresAsync();

        Task<List<Store>> GetTopRatedStoresAsync(int count);

        // =====================
        // FEED / PRODUCTS
        // =====================

        Task<List<Product>> GetFeedProductsAsync(int customerId);

        Task<List<Product>> GetStoreProductsAsync(int storeId);

        // =====================
        // FOLLOW SYSTEM
        // =====================

        Task FollowStoreAsync(int customerId, int storeId);

        Task UnfollowStoreAsync(int customerId, int storeId);

        Task<bool> IsFollowingAsync(int customerId, int storeId);

        Task<int> GetFollowersCountAsync(int storeId);

        // =====================
        // REVIEWS
        // =====================

        Task<List<Review>> GetStoreReviewsAsync(int storeId);
        Task DeleteProductReviewAsync(
     int reviewId,
     int storeOwnerId);
    }
}