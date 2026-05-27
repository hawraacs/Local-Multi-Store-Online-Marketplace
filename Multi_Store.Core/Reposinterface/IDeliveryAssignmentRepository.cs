using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryAssignmentRepository : IRepository<DeliveryAssignment>
    {
        Task<IReadOnlyList<DeliveryAssignment>> GetByOrderAsync(int orderId);

        Task<IReadOnlyList<DeliveryAssignment>> GetByDeliveryPersonAsync(int deliveryPersonId);

        Task<IReadOnlyList<DeliveryAssignment>> GetByStatusAsync(string status);

        Task<DeliveryAssignment?> GetActiveAssignmentByOrderAsync(int orderId);

        Task<IReadOnlyList<DeliveryAssignment>> GetTodayAssignmentsAsync(int deliveryPersonId);
    }
}
