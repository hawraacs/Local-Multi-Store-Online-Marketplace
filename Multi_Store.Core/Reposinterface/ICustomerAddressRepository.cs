using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICustomerAddressRepository : IRepository<CustomerAddress>
    {
        Task<IReadOnlyList<CustomerAddress>> GetByCustomerIdAsync(int customerId);

        Task<CustomerAddress?> GetDefaultAddressAsync(int customerId);

        Task<IReadOnlyList<CustomerAddress>> GetActiveAddressesAsync(int customerId);

        Task SetAllAsNonDefaultAsync(int customerId);
    }
}
