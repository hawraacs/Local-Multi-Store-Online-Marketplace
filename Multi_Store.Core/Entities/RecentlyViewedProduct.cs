namespace Multi_Store.Core.Entities
{
    public class RecentlyViewedProduct
    {
        public int Id { get; set; }

        public int CustomerID { get; set; }

        public int ProductID { get; set; }

        public DateTime ViewedAt { get; set; }

        public Customer Customer { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}