using Multi_Store.Core.DTOs;
using Multi_Store.Core.Entities;

namespace Multi_Store.Core.Interfaces
{
    public interface IPromotionManager
    {
        Task<int> SendPromotionAsync(PromotionDTO dto, int currentUserId);

        Task<List<Promotion>> GetMyStorePromotionsAsync(int currentUserId);

        Task<List<PromotionRecipient>> GetCustomerPromotionsAsync(int currentUserId);

        Task MarkAsReadAsync(int promotionRecipientId, int currentUserId);
    }
}