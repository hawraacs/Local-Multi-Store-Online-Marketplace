using Multi_Store.Core.Entities;

namespace Multi_Store.Services.Dtos
{
    public class ProductImageDTO
    {
        public int ImageID { get; set; }

        public int ProductID { get; set; }

        public string ImageURL { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public bool IsPrimary { get; set; }

        // Navigation Properties
        public Product? Product { get; set; }
    }
}