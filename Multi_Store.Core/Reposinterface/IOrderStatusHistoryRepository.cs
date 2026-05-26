using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IOrderStatusHistoryRepository : IRepository<OrderStatusHistory>
    {
        Task<IReadOnlyList<OrderStatusHistory>> GetByOrderAsync(int orderId);

        Task<OrderStatusHistory?> GetLatestStatusAsync(int orderId);
    }
}
