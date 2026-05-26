using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ISystemConfigRepository : IRepository<SystemConfig>
    {
        Task<SystemConfig?> GetByKeyAsync(string key);

        Task<IReadOnlyList<SystemConfig>> GetByValueTypeAsync(string valueType);
    }
}
