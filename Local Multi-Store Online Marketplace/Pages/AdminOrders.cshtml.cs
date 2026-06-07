using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.Text.Json;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminOrdersModel : PageModel
    {
        private readonly OrderManager _orderManager;

        public AdminOrdersModel(OrderManager orderManager)
        {
            _orderManager = orderManager;
        }

        public List<AdminOrderDto> Orders { get; set; } = new();
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingCount { get; set; }
        public int DeliveredCount { get; set; }

        public async Task OnGetAsync()
        {
            // TODO: Replace with real data from your OrderManager
            // Orders = (await _orderManager.GetAllOrdersAsync()).Select(o => new AdminOrderDto
            // {
            //     OrderId = o.OrderId,
            //     OrderNumber = o.OrderNumber,
            //     CustomerName = o.Customer?.FullName ?? "N/A",
            //     StoreName = o.Store?.StoreName ?? "N/A",
            //     TotalAmount = o.TotalAmount,
            //     Status = o.Status,
            //     PaymentStatus = o.PaymentStatus,
            //     OrderDate = o.OrderDate,
            //     Items = string.Join(", ", o.OrderItems.Select(i => $"{i.Quantity}x {i.ProductName}"))
            // }).ToList();

            // Example mock data (remove when real data is available)
            Orders = new List<AdminOrderDto>
            {
                new() { OrderId = 1, OrderNumber = "ORD-001", CustomerName = "John Doe", StoreName = "Fresh Mart", TotalAmount = 45.00m, Status = "Delivered", PaymentStatus = "Paid", OrderDate = DateTime.Now.AddDays(-2), Items = "2x Rice, 1x Chicken" },
                new() { OrderId = 2, OrderNumber = "ORD-002", CustomerName = "Jane Smith", StoreName = "Tech Zone", TotalAmount = 89.00m, Status = "Pending", PaymentStatus = "Unpaid", OrderDate = DateTime.Now, Items = "1x iPhone Case" },
                new() { OrderId = 3, OrderNumber = "ORD-003", CustomerName = "Mike Brown", StoreName = "Fashion Hub", TotalAmount = 123.50m, Status = "Confirmed", PaymentStatus = "Paid", OrderDate = DateTime.Now.AddDays(-1), Items = "1x Jacket, 2x Shirts" }
            };

            // Calculate statistics
            TotalOrders = Orders.Count;
            TotalRevenue = Orders.Sum(o => o.TotalAmount);
            PendingCount = Orders.Count(o => o.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
            DeliveredCount = Orders.Count(o => o.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class AdminOrderDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string? Items { get; set; } // Added for modal details
    }
}