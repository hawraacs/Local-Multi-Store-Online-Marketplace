using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<IReadOnlyList<Category>> GetMainCategoriesAsync();

        Task<IReadOnlyList<Category>> GetSubCategoriesAsync(int parentCategoryId);

        Task<Category?> GetCategoryWithProductsAsync(int categoryId);
    }
}
