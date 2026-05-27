using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<Order?> GetByOrderNumberAsync(string orderNumber);

        Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<Order>> GetByStatusAsync(string status);

        Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

        Task<Order?> GetOrderDetailsAsync(int orderId);

        Task<IReadOnlyList<Order>> GetRecentOrdersAsync(int count);
    }
}
