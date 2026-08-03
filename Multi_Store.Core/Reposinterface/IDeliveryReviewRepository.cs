using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryReviewRepository : IRepository<DeliveryReview>
    {
        Task<IReadOnlyList<DeliveryReview>> GetByDeliveryPersonAsync(int deliveryPersonId);

        Task<IReadOnlyList<DeliveryReview>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<DeliveryReview>> GetByStatusAsync(string status);

        Task<bool> ExistsForAssignmentAsync(int assignmentId);
    }
}