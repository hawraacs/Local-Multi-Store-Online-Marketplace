using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryPersonRepository : IRepository<DeliveryPerson>
    {
        Task<DeliveryPerson?> GetByUserIdAsync(int userId);
        Task<DeliveryPerson?> GetByRequestedByUserIdAsync(int userId);

        Task<DeliveryPerson?> GetByPhoneNumberAsync(string phoneNumber);

        Task<IReadOnlyList<DeliveryPerson>> GetAvailableAsync();

        Task<IReadOnlyList<DeliveryPerson>> GetActiveAsync();

        Task<IReadOnlyList<DeliveryPerson>> GetTopRatedAsync(int count);

        Task<DeliveryPerson?> GetWithAssignmentsAsync(int deliveryPersonId);
    }
}