public class ReviewDTO
{
    public int ReviewID { get; set; }

    public int CustomerID { get; set; }
    public string? CustomerName { get; set; }   // SIMPLE FLAT FIELD

    public int StoreID { get; set; }

    public int? ProductID { get; set; }

    public int? OrderItemID { get; set; }

    public int Rating { get; set; }
    public string? Comment { get; set; }

    public bool IsVerifiedPurchase { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}