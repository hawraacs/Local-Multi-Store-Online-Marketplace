// Entities/OrderItem.cs
namespace Multi_Store.Services.Dtos
{
    public class OrderItem
    {
        public int OrderItemID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public int StoreID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal ProductPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public bool ReviewSubmitted { get; set; } = false;

        // Navigation properties
        public virtual Order Order { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;

        // ⚠️ Review property - MAKE SURE THIS EXISTS
        public virtual Review? Review { get; set; }
    }
}