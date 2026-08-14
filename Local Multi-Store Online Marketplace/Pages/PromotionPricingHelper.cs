using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    // Single source of truth for "is there an active no-coupon sale on this
    // product, and what price does it produce" — used by CustomerProducts,
    // CustomerProductDetails, and both of their Add-to-Cart handlers. Kept
    // as one small static helper (not a new service/manager) specifically
    // so the listing price, the details price, and the price captured into
    // CartItem.PriceAtAddTime can never drift apart from one another.
    public static class PromotionPricingHelper
    {
        // A promotion counts as an active automatic sale for a product when:
        // it targets that product, it was created WITHOUT a coupon (CouponCode
        // == null — the existing coupon flow never matches this), it's still
        // marked active, and it hasn't passed its optional end date. Shared by
        // both lookup methods below so there is exactly one definition of
        // "active" for the single-product and the batch (listing page) case.
        private static IQueryable<Promotion> ActiveSalesQuery(
            ApplicationDbContext context,
            DateTime now)
        {
            return context.Promotions
                .Where(p =>
                    p.ProductID.HasValue &&
                    p.CouponCode == null &&
                    p.IsActive &&
                    p.DiscountValue != null &&
                    (p.SaleEndDate == null || p.SaleEndDate >= now));
        }

        // Used by the product details page and both Add-to-Cart handlers,
        // where only one product's price needs to be resolved.
        public static async Task<Promotion?> GetActiveSaleAsync(
            ApplicationDbContext context,
            int productId)
        {
            var now = DateTime.UtcNow;

            return await ActiveSalesQuery(context, now)
                .Where(p => p.ProductID == productId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }

        // Used by the product listing page, which shows many products at
        // once — one bulk query instead of one query per product card.
        public static async Task<Dictionary<int, Promotion>> GetActiveSalesAsync(
            ApplicationDbContext context,
            IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, Promotion>();
            }

            var now = DateTime.UtcNow;

            var matches = await ActiveSalesQuery(context, now)
                .Where(p => p.ProductID.HasValue && ids.Contains(p.ProductID.Value))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var result = new Dictionary<int, Promotion>();

            foreach (var promo in matches)
            {
                if (promo.ProductID.HasValue &&
                    !result.ContainsKey(promo.ProductID.Value))
                {
                    result[promo.ProductID.Value] = promo;
                }
            }

            return result;
        }

        // Same Percentage/Fixed convention Coupon.DiscountType already uses —
        // no new discount type introduced.
        public static decimal CalculateEffectivePrice(
            decimal originalPrice,
            Promotion? activeSale)
        {
            if (activeSale?.DiscountValue == null)
            {
                return originalPrice;
            }

            var discounted = string.Equals(
                activeSale.DiscountType,
                "Fixed",
                StringComparison.OrdinalIgnoreCase)
                ? originalPrice - activeSale.DiscountValue.Value
                : originalPrice - (originalPrice * (activeSale.DiscountValue.Value / 100m));

            return discounted < 0 ? 0 : Math.Round(discounted, 2);
        }
    }
}
