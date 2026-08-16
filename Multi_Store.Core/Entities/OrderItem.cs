// Entities/OrderItem.cs
namespace Multi_Store.Core.Entities
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

        // Fix for multi-store order confirmation: Order.Status is a single
        // field shared by every store in the order, so it cannot represent
        // "Store A confirmed, Store B still pending" at the same time.
        // This field tracks THIS item's owning store's own response
        // (Pending / Confirmed / Cancelled) to this order. All OrderItems
        // belonging to the same (OrderID, StoreID) always carry the same
        // value, since a store confirms/cancels its whole part of the
        // order at once, not item-by-item. The shared Order.Status is only
        // advanced once every participating store has responded — see
        // StoreOwner/Order/Index.cshtml.cs and Details.cshtml.cs.
        public string StoreResponseStatus { get; set; } = "Pending";

        // Navigation properties
        public virtual Order Order { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;

        // ⚠️ Review property - MAKE SURE THIS EXISTS
        public virtual Review? Review { get; set; }
    }
}