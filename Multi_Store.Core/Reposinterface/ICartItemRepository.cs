using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICartItemRepository : IRepository<CartItem>
    {
        Task<IReadOnlyList<CartItem>> GetItemsByCartAsync(int cartId);

        Task<CartItem?> GetCartItemAsync(int cartId, int productId);
    }
}
