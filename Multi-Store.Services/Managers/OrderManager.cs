using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class OrderManager
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IOrderStatusHistoryRepository _statusRepository;
        private readonly ICartRepository _cartRepository;
        private readonly ICouponRepository _couponRepository;

        public OrderManager(
            IOrderRepository orderRepository,
            IOrderItemRepository orderItemRepository,
            IOrderStatusHistoryRepository statusRepository,
            ICartRepository cartRepository,
            ICouponRepository couponRepository)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _statusRepository = statusRepository;
            _cartRepository = cartRepository;
            _couponRepository = couponRepository;
        }

        // =========================
        // CHECKOUT
        // =========================
        public async Task<int> CheckoutAsync(int customerId, int addressId, string paymentMethod, string? couponCode = null)
        {
            var cart = await _cartRepository.GetCartByCustomerAsync(customerId);

            if (cart == null || !cart.CartItems.Any())
                throw new Exception("Cart is empty");

            decimal subtotal = cart.CartItems.Sum(i => i.Quantity * i.PriceAtAddTime);
            decimal discount = 0;

            // =========================
            // COUPON LOGIC (MATCHES YOUR ENTITY)
            // =========================
            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var coupon = await _couponRepository.GetByCodeAsync(couponCode);

                if (coupon != null && coupon.IsActive)
                {
                    // date validation
                    if (coupon.StartDate <= DateTime.UtcNow &&
                        coupon.EndDate >= DateTime.UtcNow)
                    {
                        // minimum order check
                        if (coupon.MinimumOrderAmount.HasValue &&
                            subtotal < coupon.MinimumOrderAmount.Value)
                        {
                            throw new Exception("Order does not meet coupon minimum amount");
                        }

                        // usage limit check
                        if (coupon.UsageLimit.HasValue &&
                            coupon.UsedCount >= coupon.UsageLimit.Value)
                        {
                            throw new Exception("Coupon usage limit reached");
                        }

                        // calculate discount
                        if (coupon.DiscountType == "Percentage")
                        {
                            discount = subtotal * (coupon.DiscountValue / 100m);

                            if (coupon.MaximumDiscountAmount.HasValue)
                                discount = Math.Min(discount, coupon.MaximumDiscountAmount.Value);
                        }
                        else if (coupon.DiscountType == "Fixed")
                        {
                            discount = coupon.DiscountValue;
                        }

                        if (discount > subtotal)
                            discount = subtotal;

                        coupon.UsedCount++;
                        await _couponRepository.UpdateAsync(coupon);
                    }
                }
            }

            // =========================
            // TOTAL CALCULATION
            // =========================
            decimal tax = subtotal * 0.14m;
            decimal total = subtotal - discount + tax;

            // =========================
            // CREATE ORDER
            // =========================
            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(),
                CustomerID = customerId,
                AddressID = addressId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                PaymentMethod = paymentMethod,
                PaymentStatus = "Unpaid",
                Subtotal = subtotal,
                DiscountAmount = discount,
                TaxAmount = tax,
                TotalAmount = total
            };

            await _orderRepository.AddAsync(order);

            // =========================
            // ORDER ITEMS
            // =========================
            foreach (var item in cart.CartItems)
            {
                await _orderItemRepository.AddAsync(new OrderItem
                {
                    OrderID = order.OrderID,
                    ProductID = item.ProductID,
                    StoreID = item.Product.StoreID,
                    ProductName = item.Product.ProductName,
                    ProductPrice = item.PriceAtAddTime,
                    Quantity = item.Quantity,
                    TotalPrice = item.Quantity * item.PriceAtAddTime

                });
            }

            // =========================
            // STATUS HISTORY
            // =========================
            await _statusRepository.AddAsync(new OrderStatusHistory
            {
                OrderID = order.OrderID,
                PreviousStatus = "",
                NewStatus = "Pending",
                ChangedBy = "System",
                ChangedAt = DateTime.UtcNow,
                Notes = "Checkout completed"
            });

            // =========================
            // CLEAR CART
            // =========================
            foreach (var item in cart.CartItems.ToList())
            {
                cart.CartItems.Remove(item);
            }

            return order.OrderID;
        }

        // =========================
        // CANCEL ORDER
        // =========================
        public async Task CancelOrderAsync(int orderId, string reason)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            if (order.Status == "Shipped" || order.Status == "Delivered")
                throw new Exception("Cannot cancel shipped/delivered order");

            var oldStatus = order.Status;

            order.Status = "Cancelled";
            order.CancellationReason = reason;
            order.CancelledAt = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);

            await _statusRepository.AddAsync(new OrderStatusHistory
            {
                OrderID = orderId,
                PreviousStatus = oldStatus,
                NewStatus = "Cancelled",
                ChangedBy = "Customer",
                ChangedAt = DateTime.UtcNow,
                Notes = reason
            });
        }

        // =========================
        // TRACK ORDER
        // =========================
        public async Task<Order> GetOrderDetailsAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            return order;
        }

        // =========================
        // UPDATE STATUS
        // =========================
        public async Task UpdateOrderStatusAsync(int orderId, string newStatus, string changedBy)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            var oldStatus = order.Status;

            order.Status = newStatus;

            await _orderRepository.UpdateAsync(order);

            await _statusRepository.AddAsync(new OrderStatusHistory
            {
                OrderID = orderId,
                PreviousStatus = oldStatus,
                NewStatus = newStatus,
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow
            });
        }

        // =========================
        // ORDER NUMBER
        // =========================
        private string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
        }
    }
}
