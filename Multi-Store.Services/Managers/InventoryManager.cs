using Multi_Store.Core.Reposinterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class InventoryManager
    {
        private readonly IProductRepository _productRepository;

        public InventoryManager(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        // =========================
        // UPDATE STOCK (INCREASE / DECREASE)
        // =========================
        public async Task UpdateStockAsync(int productId, int quantityChange)
        {
            var product = await _productRepository.GetByIdAsync(productId);

            if (product == null)
                throw new Exception("Product not found");

            product.Quantity += quantityChange;

            if (product.Quantity < 0)
                product.Quantity = 0;

            await _productRepository.UpdateAsync(product);
        }

        // =========================
        // CHECK LOW STOCK
        // =========================
        public async Task<bool> IsLowStockAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);

            if (product == null)
                throw new Exception("Product not found");

            return product.Quantity <= product.LowStockThreshold;
        }

        // =========================
        // RESTORE STOCK
        // =========================
        public async Task RestoreStockAsync(int productId, int quantity)
        {
            if (quantity <= 0)
                throw new Exception("Restore quantity must be greater than 0");

            var product = await _productRepository.GetByIdAsync(productId);

            if (product == null)
                throw new Exception("Product not found");

            product.Quantity += quantity;

            await _productRepository.UpdateAsync(product);
        }
    }
}
