using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminCategoriesModel : PageModel
    {
        private readonly CategoryManager _categoryManager;

        public AdminCategoriesModel(CategoryManager categoryManager)
        {
            _categoryManager = categoryManager;
        }

        public List<CategoryViewModel> Categories { get; set; } = new();

        // Statistics properties
        public int TotalCategories => Categories?.Count ?? 0;
        public int ParentCategoriesCount => Categories?.Count(c => c.ParentName == "None" || string.IsNullOrEmpty(c.ParentName)) ?? 0;
        public int SubcategoriesCount => TotalCategories - ParentCategoriesCount;

        public async Task OnGetAsync()
        {
            var categories = await _categoryManager.GetAllCategoriesAsync();

            Categories = categories.Select(c => new CategoryViewModel
            {
                CategoryId = c.CategoryID,
                Name = c.CategoryName,
                Slug = c.CategorySlug,
                ParentName = c.ParentCategory?.CategoryName ?? "None"
            }).ToList();
        }

        public async Task<IActionResult> OnPostCreateOrUpdateAsync(
            int? id,
            string name,
            int? parentId)
        {
            if (id.HasValue && id > 0)
            {
                var category = await _categoryManager.GetByIdAsync(id.Value);

                if (category != null)
                {
                    category.CategoryName = name;
                    category.ParentCategoryID = parentId;
                    await _categoryManager.UpdateCategoryAsync(category);
                }
            }
            else
            {
                var category = new CategoryDTO
                {
                    CategoryName = name,
                    ParentCategoryID = parentId,
                    IsActive = true
                };

                await _categoryManager.AddCategoryAsync(category);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _categoryManager.DeleteCategoryAsync(id);
            return RedirectToPage();
        }
    }

    public class CategoryViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
    }
}