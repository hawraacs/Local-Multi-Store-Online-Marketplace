using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class Admin1Model : PageModel
    {
        // Replace these with real data from your services
        public decimal TotalRevenue { get; set; } = 124530m;
        public int TotalOrders { get; set; } = 1234;
        public int ActiveStores { get; set; } = 45;
        public int TotalUsers { get; set; } = 2891;
        public List<RecentOrderDto> RecentOrders { get; set; } = new();

        public void OnGet()
        {
            // Example data – replace with database query
            RecentOrders = new List<RecentOrderDto>
            {
                new() { OrderNumber = "ORD-001", CustomerName = "John Doe", StoreName = "Fresh Mart", Amount = 45.00m, Status = "Delivered" },
                new() { OrderNumber = "ORD-002", CustomerName = "Jane Smith", StoreName = "Tech Zone", Amount = 89.00m, Status = "Pending" },
                new() { OrderNumber = "ORD-003", CustomerName = "Mike Brown", StoreName = "Fashion Hub", Amount = 23.00m, Status = "Delivered" }
            };
        }
    }

    public class RecentOrderDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}