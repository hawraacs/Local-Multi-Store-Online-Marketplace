using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Multi_Store.Pages.Customer
{
    public class OrderHistoryModel : PageModel
    {
        private readonly OrderManager _orderManager;

        public OrderHistoryModel(OrderManager orderManager)
        {
            _orderManager = orderManager;
        }

        public List<OrderDTO> Orders { get; set; } = new();

        public async Task OnGetAsync()
        {
            int customerId = GetCurrentCustomerId();
            Orders = await _orderManager.GetCustomerOrdersAsync(customerId);
        }

        private int GetCurrentCustomerId()
        {
            return 1;
        }
    }
}