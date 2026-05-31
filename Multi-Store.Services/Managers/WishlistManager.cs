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
                ProductID = w.ProductID,
             
              
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