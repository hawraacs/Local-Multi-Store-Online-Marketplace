using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;

namespace Multi_Store.Infrastructure.Repositories
{
    public class PromotionRepository : IPromotionRepository
    {
        private readonly ApplicationDbContext _context;

        public PromotionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int?> GetStoreIdByOwnerUserIdAsync(int ownerUserId)
        {
            return await _context.Stores
                .Where(s => s.OwnerUserID == ownerUserId)
                .Select(s => (int?)s.StoreID)
                .FirstOrDefaultAsync();
        }

        public async Task<List<int>> GetAllCustomerIdsAsync()
        {
            return await _context.Customers
                .Select(c => c.CustomerID)
                .Distinct()
                .ToListAsync();
        }
        public async Task<List<int>> GetAudienceCustomerIdsAsync(string audienceType, int storeId)
        {
            if (audienceType == "PreviousCustomers")
            {
                return await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.StoreID == storeId && oi.Order != null)
                    .Select(oi => oi.Order.CustomerID)
                    .Distinct()
                    .ToListAsync();
            }

            if (audienceType == "WishlistCustomers")
            {
                return await (
                    from wishlist in _context.Wishlists
                    join product in _context.Products
                        on wishlist.ProductID equals product.ProductID
                    where product.StoreID == storeId
                    select wishlist.CustomerID
                )
                .Distinct()
                .ToListAsync();
            }

            return await GetAllCustomerIdsAsync();
        }

        public async Task AddPromotionWithRecipientsAsync(Promotion promotion, List<int> customerIds)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Promotions.AddAsync(promotion);
                await _context.SaveChangesAsync();

                var recipients = customerIds.Select(customerId => new PromotionRecipient
                {
                    PromotionID = promotion.PromotionID,
                    CustomerID = customerId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _context.PromotionRecipients.AddRangeAsync(recipients);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Promotion>> GetPromotionsByStoreIdAsync(int storeId)
        {
            return await _context.Promotions
                .Where(p => p.StoreID == storeId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<PromotionRecipient>> GetPromotionsByCustomerUserIdAsync(int userId)
        {
            return await _context.PromotionRecipients
                .Include(pr => pr.Promotion)
                    .ThenInclude(p => p!.Store)
                .Include(pr => pr.Customer)
                .Where(pr => pr.Customer != null && pr.Customer.UserID == userId)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int promotionRecipientId, int userId)
        {
            var recipient = await _context.PromotionRecipients
                .Include(pr => pr.Customer)
                .FirstOrDefaultAsync(pr =>
                    pr.PromotionRecipientID == promotionRecipientId &&
                    pr.Customer != null &&
                    pr.Customer.UserID == userId);

            if (recipient == null)
                return;

            recipient.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CouponCodeExistsAsync(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
                return false;

            string cleanCode = couponCode.Trim().ToUpper();

            return await _context.Coupons
                .AnyAsync(c => c.CouponCode.ToUpper() == cleanCode);
        }

        public async Task AddCouponAsync(Coupon coupon)
        {
            await _context.Coupons.AddAsync(coupon);
            await _context.SaveChangesAsync();
        }
    }
}