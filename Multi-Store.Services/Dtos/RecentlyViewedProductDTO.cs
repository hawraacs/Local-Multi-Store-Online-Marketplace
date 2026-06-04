namespace Multi_Store.Services.Dtos
{
    public class RecentlyViewedProductDTO
    {
        public int Id { get; set; }

        public int CustomerID { get; set; }

        public int ProductID { get; set; }

        public DateTime ViewedAt { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = "/images/no-image.png";

        public bool IsOutOfStock { get; set; }
    }
}