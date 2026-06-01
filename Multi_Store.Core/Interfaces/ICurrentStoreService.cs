using com.sun.xml.@internal.rngom.digested;
using Multi_Store.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Multi_Store.Core.Entities;
using System.Threading.Tasks;

namespace Multi_Store.Core.Interfaces
{
    public interface ICurrentStoreService
    {
        Task<Store?> GetCurrentStoreAsync();
        Task<int?> GetCurrentStoreIdAsync();
        Task<bool> IsStoreOwnerAsync();
        Task<bool> ValidateStoreAccessAsync(int storeId);
    }
}