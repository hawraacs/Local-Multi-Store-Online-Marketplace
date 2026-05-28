// ==========================================================
// IADMINREPOSITORY
// File: Core/Reposinterface/IAdminRepository.cs
// ==========================================================

using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Store.Infrastructure.Repositories
{
    public interface IAdminRepository : IRepository<Admin>
    {
        Task<IReadOnlyList<Admin>> GetByRoleAsync(string role);

        Task<Admin?> GetByUserIdAsync(int userId);

        Task<IReadOnlyList<Admin>> GetLatestAdminsAsync(int count);
    }
}