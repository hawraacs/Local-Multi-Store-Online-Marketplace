using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IWishlistRepository : IRepository<Wishlist>
    {
        Task<IReadOnlyList<Wishlist>> GetByCustomerAsync(int customerId);

        Task<Wishlist?> GetByCustomerAndProductAsync(
            int customerId,
            int productId);

        Task<bool> ExistsAsync(
            int customerId,
            int productId);
    }
}
