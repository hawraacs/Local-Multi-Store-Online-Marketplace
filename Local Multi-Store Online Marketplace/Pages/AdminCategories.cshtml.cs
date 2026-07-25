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

        public List<CategoryViewModel> Categories { get; set; } = new();   // flat list (used for stats)
        public List<CategoryViewModel> RootCategories { get; set; } = new();     // nested tree (roots only; each has .Children populated)
        public List<CategoryViewModel> ParentDropdownOptions { get; set; } = new(); // tree-ordered flat list, for the "Parent Category" <select>

        // Statistics properties
        public int TotalCategories => Categories?.Count ?? 0;
        public int ParentCategoriesCount => Categories?.Count(c => c.ParentId == null) ?? 0;
        public int SubcategoriesCount => TotalCategories - ParentCategoriesCount;

        public async Task OnGetAsync()
        {
            var categories = await _categoryManager.GetAllCategoriesAsync();

            var flat = categories
                .Where(c => c.IsActive)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryID,
                    Name = c.CategoryName,
                    Slug = c.CategorySlug,
                    ParentName = c.ParentCategory?.CategoryName ?? "None",
                    ParentId = c.ParentCategoryID
                })
                .ToList();

            Categories = flat;

            var byParent = flat.ToLookup(c => c.ParentId);

            // Recursively attach children, depth (for dropdown indentation),
            // and a "Grandparent › Parent › Name" breadcrumb path.
            void Attach(CategoryViewModel node, int depth, string parentPath)
            {
                node.Depth = depth;
                node.Path = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} › {node.Name}";
                node.Children = byParent[node.CategoryId].OrderBy(c => c.Name).ToList();

                foreach (var child in node.Children)
                {
                    Attach(child, depth + 1, node.Path);
                }
            }

            RootCategories = byParent[null].OrderBy(c => c.Name).ToList();
            foreach (var root in RootCategories)
            {
                Attach(root, 0, "");
            }

            // Depth-first flattening of the tree, so the parent dropdown lists
            // categories in the same order/indentation as the tree above it.
            var ordered = new List<CategoryViewModel>();
            void Flatten(CategoryViewModel node)
            {
                ordered.Add(node);
                foreach (var child in node.Children)
                {
                    Flatten(child);
                }
            }
            foreach (var root in RootCategories)
            {
                Flatten(root);
            }
            ParentDropdownOptions = ordered;
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
        public int? ParentId { get; set; }            // null if no parent

        // Populated in OnGetAsync while building the tree - not set by the DB query itself.
        public List<CategoryViewModel> Children { get; set; } = new();
        public int Depth { get; set; }                 // 0 = top level, used to indent the dropdown
        public string Path { get; set; } = string.Empty; // e.g. "Electronics › Phones"
    }
}
