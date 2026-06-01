namespace Multi_Store.Core.Entities
{
    public class ProductImage
    {
        public int ImageID { get; set; }

        public int ProductID { get; set; }

        public string ImageURL { get; set; } = string.Empty;
        public string ImageUrl { get; set; }
        public int DisplayOrder { get; set; }

        public bool IsPrimary { get; set; }

        // Navigation Properties
        public Product? Product { get; set; }
    }
}