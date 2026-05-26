using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<Product?> GetBySlugAsync(string slug);

        Task<IReadOnlyList<Product>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<Product>> GetByCategoryAsync(int categoryId);

        Task<IReadOnlyList<Product>> GetActiveProductsAsync();

        Task<IReadOnlyList<Product>> GetLowStockProductsAsync();

        Task<IReadOnlyList<Product>> GetTopRatedProductsAsync(int count);

        Task<IReadOnlyList<Product>> SearchProductsAsync(string keyword);

        Task<Product?> GetProductDetailsAsync(int productId);
    }
}
