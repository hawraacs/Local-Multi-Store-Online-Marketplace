using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class OrderHistoryManager
    {
        private readonly IOrderRepository _orderRepository;

        public OrderHistoryManager(
            IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<List<OrderDTO>> GetCustomerOrdersAsync(int customerId)
        {
            var orders =
                await _orderRepository.GetByCustomerAsync(customerId);

            return orders.Select(o => new OrderDTO
            {
                OrderID = o.OrderID,
                OrderNumber = o.OrderNumber,
                CustomerID = o.CustomerID,
                AddressID = o.AddressID,
                OrderDate = o.OrderDate,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                PaymentStatus = o.PaymentStatus,
                TotalAmount = o.TotalAmount
            }).ToList();
        }
    }
}