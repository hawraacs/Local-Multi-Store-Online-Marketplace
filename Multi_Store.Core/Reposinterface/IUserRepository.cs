using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{

    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);

        Task<User?> GetByPhoneAsync(string phoneNumber);

        

        Task<IReadOnlyList<User>> GetActiveUsersAsync();

        Task<bool> EmailExistsAsync(string email);

        Task<bool> PhoneExistsAsync(string phoneNumber);
    }
}
