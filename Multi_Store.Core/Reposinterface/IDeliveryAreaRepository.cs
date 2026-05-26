using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IDeliveryAreaRepository : IRepository<DeliveryArea>
    {
        Task<IReadOnlyList<DeliveryArea>> GetByStoreAsync(int storeId);

        Task<IReadOnlyList<DeliveryArea>> GetActiveAreasAsync(int storeId);

        Task<DeliveryArea?> GetByAreaNameAsync(int storeId, string areaName);

        Task<IReadOnlyList<DeliveryArea>> GetFreeDeliveryAreasAsync(int storeId);
    }
}
