namespace Multi_Store.Core.Entities
{
    public class ProductImage
    {
        public int ImageID { get; set; }

        public int ProductID { get; set; }

        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        public bool IsPrimary { get; set; }

        // Navigation Properties
        public Product? Product { get; set; }
    }
}