using System.Collections.Generic;

namespace Multi_Store.Core.Entities
{
    public class Category
    {
        public int CategoryID { get; set; }

        public int? ParentCategoryID { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string CategorySlug { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        // Navigation Properties
        public Category? ParentCategory { get; set; }

        public ICollection<Category>? SubCategories { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}