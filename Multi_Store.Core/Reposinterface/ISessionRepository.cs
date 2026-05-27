using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ISessionRepository : IRepository<Session>
    {
        Task<Session?> GetByTokenAsync(string token);

        Task<IReadOnlyList<Session>> GetByUserAsync(int userId);

        Task<IReadOnlyList<Session>> GetActiveSessionsAsync(int userId);

        Task RemoveExpiredSessionsAsync();
    }
}
