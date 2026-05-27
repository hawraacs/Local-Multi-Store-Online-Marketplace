using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface INotificationRepository : IRepository<Notification>
    {
        Task<IReadOnlyList<Notification>> GetByUserAsync(int userId);

        Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(int userId);

        Task<IReadOnlyList<Notification>> GetByTypeAsync(int userId, string type);

        Task<int> GetUnreadCountAsync(int userId);
    }
}
