using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class CartManager
    {
        private readonly ICartRepository _cartRepository;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IProductRepository _productRepository;

        public CartManager(
            ICartRepository cartRepository,
            ICartItemRepository cartItemRepository,
            IProductRepository productRepository)
        {
            _cartRepository = cartRepository;
            _cartItemRepository = cartItemRepository;
            _productRepository = productRepository;
        }

        // =========================
        // GET OR CREATE CART
        // =========================
        private async Task<Cart> GetOrCreateCartAsync(int? customerId, string? sessionToken)
        {
            var cart = customerId.HasValue
                ? await _cartRepository.GetCartByCustomerAsync(customerId.Value)
                : await _cartRepository.GetCartBySessionTokenAsync(sessionToken);

            if (cart != null)
                return cart;

            var newCart = new Cart
            {
                CustomerID = customerId,
                SessionToken = sessionToken,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await _cartRepository.AddAsync(newCart);

            return newCart;
        }

        // =========================
        // ADD TO CART
        // =========================
        public async Task AddToCartAsync(int productId, int quantity, int? customerId, string? sessionToken)
        {
            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0");

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new Exception("Product not found");

            var cart = await GetOrCreateCartAsync(customerId, sessionToken);

            var existingItem = cart.CartItems
                .FirstOrDefault(i => i.ProductID == productId);

            int existingQty = existingItem?.Quantity ?? 0;

            if (product.Quantity < existingQty + quantity)
                throw new Exception("Not enough stock available");

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                await _cartItemRepository.AddAsync(new CartItem
                {
                    CartID = cart.CartID,
                    ProductID = productId,
                    Quantity = quantity,
                    PriceAtAddTime = product.Price
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
        }

        // =========================
        // REMOVE FROM CART
        // =========================
        public async Task RemoveFromCartAsync(int productId, int? customerId, string? sessionToken)
        {
            var cart = await GetOrCreateCartAsync(customerId, sessionToken);

            var item = cart.CartItems
                .FirstOrDefault(i => i.ProductID == productId);

            if (item == null)
                throw new Exception("Item not found in cart");

            await _cartItemRepository.DeleteAsync(item);

            cart.UpdatedAt = DateTime.UtcNow;
        }

        // =========================
        // UPDATE QUANTITY
        // =========================
        public async Task UpdateQuantityAsync(int productId, int quantity, int? customerId, string? sessionToken)
        {
            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0");

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new Exception("Product not found");

            var cart = await GetOrCreateCartAsync(customerId, sessionToken);

            var item = cart.CartItems
                .FirstOrDefault(i => i.ProductID == productId);

            if (item == null)
                throw new Exception("Item not found in cart");

            if (product.Quantity < quantity)
                throw new Exception("Not enough stock available");

            item.Quantity = quantity;

            cart.UpdatedAt = DateTime.UtcNow;
        }

        // =========================
        // CLEAR CART
        // =========================
        public async Task ClearCartAsync(int? customerId, string? sessionToken)
        {
            var cart = await GetOrCreateCartAsync(customerId, sessionToken);

            foreach (var item in cart.CartItems.ToList())
            {
                await _cartItemRepository.DeleteAsync(item);
            }

            cart.UpdatedAt = DateTime.UtcNow;
        }
    }
}
