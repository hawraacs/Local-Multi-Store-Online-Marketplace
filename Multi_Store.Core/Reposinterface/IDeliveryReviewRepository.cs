using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryReviewRepository : IRepository<DeliveryReview>
    {
        // Enforces "one delivery review per order" at the application
        // level (mirrors ReviewRepository.ExistsForOrderItemAsync).
        Task<bool> ExistsForOrderAsync(int orderId);

        // Used to recalculate DeliveryPerson.Rating after a new review
        // is saved.
        Task<IReadOnlyList<DeliveryReview>> GetByDeliveryPersonAsync(int deliveryPersonId);
    }
}

