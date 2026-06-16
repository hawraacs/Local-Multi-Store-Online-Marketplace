using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;

namespace Multi_Store.Core.Reposinterface
{
    public interface ISessionRepository : IRepository<Session>
    {
        Task<Session?> GetByTokenAsync(string token);

        Task<IReadOnlyList<Session>> GetByUserAsync(int userId);

        Task<IReadOnlyList<Session>> GetActiveSessionsAsync(int userId);

        Task<Session?> GetActiveByUserIdAsync(int userId);

        Task UpdateLastActivityAsync(int userId);

        Task RemoveExpiredSessionsAsync();
    }
}