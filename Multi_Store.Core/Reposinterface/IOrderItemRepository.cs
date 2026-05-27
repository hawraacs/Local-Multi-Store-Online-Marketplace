using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IOrderItemRepository : IRepository<OrderItem>
    {
        Task<IReadOnlyList<OrderItem>> GetByOrderAsync(int orderId);

        Task<IReadOnlyList<OrderItem>> GetByProductAsync(int productId);

        Task<IReadOnlyList<OrderItem>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<OrderItem>> GetPendingReviewItemsAsync(int customerId);
    }
}
