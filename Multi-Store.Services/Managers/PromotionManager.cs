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

            var customerIds = await _promotionRepository.GetAudienceCustomerIdsAsync(
    dto.AudienceType,
    storeId.Value);

            if (customerIds.Count == 0)
                throw new InvalidOperationException("No customers found for the selected audience.");

            string? cleanCouponCode = string.IsNullOrWhiteSpace(dto.CouponCode)
                ? null
                : dto.CouponCode.Trim().ToUpper();

            if (dto.CreateCoupon)
            {
                if (string.IsNullOrWhiteSpace(cleanCouponCode))
                    throw new InvalidOperationException("Coupon code is required when creating a usable coupon.");

                if (dto.DiscountValue <= 0)
                    throw new InvalidOperationException("Discount value must be greater than zero.");

                if (dto.DiscountType != "Percentage" && dto.DiscountType != "Fixed")
                    throw new InvalidOperationException("Invalid discount type.");

                if (dto.DiscountType == "Percentage" && dto.DiscountValue > 100)
                    throw new InvalidOperationException("Percentage discount cannot be greater than 100%.");

                if (dto.CouponEndDate == null)
                    throw new InvalidOperationException("Coupon end date is required.");

                if (dto.CouponEndDate.Value.Date < DateTime.Today)
                    throw new InvalidOperationException("Coupon end date cannot be in the past.");

                if (dto.UsageLimit != null && dto.UsageLimit <= 0)
                    throw new InvalidOperationException("Usage limit must be greater than zero.");

                if (dto.UsagePerCustomerLimit != null && dto.UsagePerCustomerLimit <= 0)
                    throw new InvalidOperationException("Usage per customer limit must be greater than zero.");

                bool couponExists = await _promotionRepository.CouponCodeExistsAsync(cleanCouponCode);

                if (couponExists)
                    throw new InvalidOperationException("This coupon code already exists. Please choose another code.");

                var coupon = new Coupon
                {
                    StoreID = storeId.Value,
                    CouponCode = cleanCouponCode,
                    DiscountType = dto.DiscountType,
                    DiscountValue = dto.DiscountValue,
                    MinimumOrderAmount = dto.MinimumOrderAmount ?? 0,
                    MaximumDiscountAmount = dto.MaximumDiscountAmount,
                    StartDate = DateTime.UtcNow,
                    EndDate = dto.CouponEndDate.Value.Date.AddDays(1).AddTicks(-1),
                    UsageLimit = dto.UsageLimit ?? 100,
                    UsagePerCustomerLimit = dto.UsagePerCustomerLimit ?? 1,
                    UsedCount = 0,
                    IsActive = true
                };

                await _promotionRepository.AddCouponAsync(coupon);
            }

            var promotion = new Promotion
            {
                StoreID = storeId.Value,
                CreatedByUserID = currentUserId,
                Title = dto.Title.Trim(),
                Message = dto.Message.Trim(),
                AudienceType = dto.AudienceType,
                CouponCode = cleanCouponCode,
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