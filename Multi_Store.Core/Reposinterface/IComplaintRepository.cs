using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IComplaintRepository : IRepository<Complaint>
    {
        Task<IReadOnlyList<Complaint>> GetByCustomerAsync(int customerId);

        Task<IReadOnlyList<Complaint>> GetByStatusAsync(string status);

        Task<IReadOnlyList<Complaint>> GetByOrderAsync(int orderId);

        Task<IReadOnlyList<Complaint>> GetByProductAsync(int productId);

        Task<IReadOnlyList<Complaint>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<Complaint>> GetLatestAsync(int count);
    }
}
