using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IStoreRepository : IRepository<Store>
    {
        Task<Store?> GetByCodeAsync(string storeCode);

        Task<IReadOnlyList<Store>> GetByStatusAsync(string status);

        Task<IReadOnlyList<Store>> GetApprovedStoresAsync();

        Task<IReadOnlyList<Store>> GetTopRatedStoresAsync(int count);

        Task<Store?> GetStoreDetailsAsync(int storeId);

        Task<IReadOnlyList<Store>> SearchStoresAsync(string keyword);
    }
}
