using Multi_Store.Core.Entities;

namespace Multi_Store.Core.Interfaces
{
    public interface IPromotionRepository
    {
        Task<int?> GetStoreIdByOwnerUserIdAsync(int ownerUserId);

        Task<List<int>> GetAllCustomerIdsAsync();

        Task<List<int>> GetAudienceCustomerIdsAsync(string audienceType, int storeId);

        Task AddPromotionWithRecipientsAsync(Promotion promotion, List<int> customerIds);

        Task<List<Promotion>> GetPromotionsByStoreIdAsync(int storeId);

        Task<List<PromotionRecipient>> GetPromotionsByCustomerUserIdAsync(int userId);

        Task MarkAsReadAsync(int promotionRecipientId, int userId);

        Task<bool> CouponCodeExistsAsync(string couponCode);
        Task<List<int>> GetUserIdsByCustomerIdsAsync(List<int> customerIds);

        Task AddCouponAsync(Coupon coupon);
        Task DeletePromotionAsync(int id);
    }
}