using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IRefundRequestRepository : IRepository<RefundRequest>
    {
        Task<IReadOnlyList<RefundRequest>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<RefundRequest>> GetByStatusAsync(string status);

        Task<RefundRequest?> GetByOrderAsync(int orderId);

        Task<IReadOnlyList<RefundRequest>> GetPendingRequestsAsync();
    }
}
