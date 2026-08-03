using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryFollowRepository : IRepository<DeliveryFollow>
    {
        Task<DeliveryFollow?> GetByCustomerAndDeliveryPersonAsync(int customerId, int deliveryPersonId);

        Task<IReadOnlyList<DeliveryFollow>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<DeliveryFollow>> GetByDeliveryPersonAsync(int deliveryPersonId);

        Task<int> GetFollowersCountAsync(int deliveryPersonId);

        Task<bool> IsFollowingAsync(int customerId, int deliveryPersonId);
    }
}