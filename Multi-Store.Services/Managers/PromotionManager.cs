using Multi_Store.Core.DTOs;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;

namespace Multi_Store.Core.Managers
{
    public class PromotionManager : IPromotionManager
    {
        private readonly IPromotionRepository _promotionRepository;

        public PromotionManager(IPromotionRepository promotionRepository)
        {
            _promotionRepository = promotionRepository;
        }

        public async Task<int> SendPromotionAsync(PromotionDTO dto, int currentUserId)
        {
            var storeId = await _promotionRepository.GetStoreIdByOwnerUserIdAsync(currentUserId);

            if (storeId == null)
                throw new InvalidOperationException("You do not have a store connected to your account.");

            var customerIds = await _promotionRepository.GetAllCustomerIdsAsync();

            if (customerIds.Count == 0)
                throw new InvalidOperationException("No customers found to receive this promotion.");

            var promotion = new Promotion
            {
                StoreID = storeId.Value,
                CreatedByUserID = currentUserId,
                Title = dto.Title.Trim(),
                Message = dto.Message.Trim(),
                AudienceType = dto.AudienceType,
                CouponCode = string.IsNullOrWhiteSpace(dto.CouponCode) ? null : dto.CouponCode.Trim(),
                RecipientCount = customerIds.Count,
                IsSent = true,
                Status = "Sent",
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };

            await _promotionRepository.AddPromotionWithRecipientsAsync(promotion, customerIds);

            return customerIds.Count;
        }

        public async Task<List<Promotion>> GetMyStorePromotionsAsync(int currentUserId)
        {
            var storeId = await _promotionRepository.GetStoreIdByOwnerUserIdAsync(currentUserId);

            if (storeId == null)
                return new List<Promotion>();

            return await _promotionRepository.GetPromotionsByStoreIdAsync(storeId.Value);
        }

        public async Task<List<PromotionRecipient>> GetCustomerPromotionsAsync(int currentUserId)
        {
            return await _promotionRepository.GetPromotionsByCustomerUserIdAsync(currentUserId);
        }

        public async Task MarkAsReadAsync(int promotionRecipientId, int currentUserId)
        {
            await _promotionRepository.MarkAsReadAsync(promotionRecipientId, currentUserId);
        }
    }
}