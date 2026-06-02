using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class WishlistManager
    {
        private readonly IWishlistRepository _wishlistRepository;

        public WishlistManager(IWishlistRepository wishlistRepository)
        {
            _wishlistRepository = wishlistRepository;
        }

        // =========================
        // GET CUSTOMER WISHLIST
        // =========================
        public async Task<List<WishlistDTO>> GetCustomerWishlistAsync(int customerId)
        {
            var data = await _wishlistRepository.GetByCustomerAsync(customerId);

            return data.Select(w => new WishlistDTO
            {
                WishlistID = w.WishlistID,
                CustomerID = w.CustomerID,
                ProductID = w.ProductID,
                AddedAt = w.AddedAt,

                ProductName = w.Product.ProductName,
                Price = w.Product.Price,
                StoreName = w.Product.Store.StoreName,
                IsOutOfStock = w.Product.IsOutOfStock,

                ImageUrl = w.Product.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault() ?? "/images/no-image.png"
            }).ToList();
        }

        // =========================
        // ADD TO WISHLIST
        // =========================
        public async Task AddToWishlistAsync(int customerId, int productId)
        {
            var exists = await _wishlistRepository.ExistsAsync(customerId, productId);

            if (exists)
                return;

            var wishlist = new Wishlist
            {
                CustomerID = customerId,
                ProductID = productId,
                AddedAt = DateTime.UtcNow
            };

            await _wishlistRepository.AddAsync(wishlist);
        }

        // =========================
        // REMOVE FROM WISHLIST
        // =========================
        public async Task RemoveFromWishlistAsync(int customerId, int productId)
        {
            var item = await _wishlistRepository
                .GetByCustomerAndProductAsync(customerId, productId);

            if (item != null)
            {
                await _wishlistRepository.DeleteAsync(item);
            }
        }

        // =========================
        // CHECK IF IN WISHLIST
        // =========================
        public async Task<bool> IsInWishlistAsync(int customerId, int productId)
        {
            return await _wishlistRepository.ExistsAsync(customerId, productId);
        }
    }
}