using Multi_Store.Core.Reposinterface;

namespace Multi_Store.Services.Managers
{
    public class SessionManager
    {
        private readonly ISessionRepository _repo;

        public SessionManager(ISessionRepository repo)
        {
            _repo = repo;
        }

        public async Task<bool> IsUserOnlineAsync(int userId)
        {
            var session = await _repo.GetActiveByUserIdAsync(userId);

            return session != null &&
                   session.IsActive &&
                   session.ExpiresAt > DateTime.UtcNow;
        }

        public async Task TouchAsync(int userId)
        {
            await _repo.UpdateLastActivityAsync(userId);
        }
    }
}