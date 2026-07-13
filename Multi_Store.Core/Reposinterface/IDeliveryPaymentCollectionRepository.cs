using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryPaymentCollectionRepository : IRepository<DeliveryPaymentCollection>
    {
        Task<DeliveryPaymentCollection?> GetByOrderAsync(int orderId);

        Task<System.Collections.Generic.IReadOnlyList<DeliveryPaymentCollection>>
            GetByDeliveryPersonAsync(int deliveryPersonId);
    }
}