using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<IReadOnlyList<Payment>> GetByOrderAsync(int orderId);

        Task<IReadOnlyList<Payment>> GetByStatusAsync(string status);

        Task<Payment?> GetLatestPaymentAsync(int orderId);

        Task<Payment?> GetByTransactionIdAsync(string transactionId);
    }
}
