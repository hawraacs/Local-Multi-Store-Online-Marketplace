using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICustomerRepository : IRepository<Customer>
    {
        Task<Customer?> GetByUserIdAsync(int userId);

        Task<Customer?> GetWithAddressesAsync(int customerId);

        Task<Customer?> GetWithOrdersAsync(int customerId);

        Task<IReadOnlyList<Customer>> GetTopCustomersAsync(int count);

        Task<IReadOnlyList<Customer>> GetBlockedCODCustomersAsync();
    }
}
