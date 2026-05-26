using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IReviewRepository : IRepository<Review>
    {
        Task<IReadOnlyList<Review>> GetByProductAsync(int productId);

        Task<IReadOnlyList<Review>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<Review>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<Review>> GetByStatusAsync(string status);

        Task<bool> ExistsForOrderItemAsync(int orderItemId);
    }
}
