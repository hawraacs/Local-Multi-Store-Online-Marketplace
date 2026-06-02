using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class CustomerOrdersModel : PageModel
    {
        private readonly OrderHistoryManager _orderManager;

        public List<OrderDTO> Orders { get; set; } = new();

        public CustomerOrdersModel(
            OrderHistoryManager orderManager)
        {
            _orderManager = orderManager;
        }

        public async Task OnGetAsync()
        {
            Orders =
                await _orderManager
                .GetCustomerOrdersAsync(1);
        }
    }
}