using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IProductImageRepository : IRepository<ProductImage>
    {
        Task<IReadOnlyList<ProductImage>> GetByProductAsync(int productId);

        Task<ProductImage?> GetPrimaryImageAsync(int productId);

        Task<int> GetMaxDisplayOrderAsync(int productId);
    }
}
