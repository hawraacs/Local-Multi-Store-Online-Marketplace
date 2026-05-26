using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICartRepository : IRepository<Cart>
    {
        Task<Cart?> GetCartByCustomerAsync(int customerId);

        Task<Cart?> GetCartBySessionTokenAsync(string sessionToken);

        Task<Cart?> GetCartWithItemsAsync(int cartId);
    }
}
