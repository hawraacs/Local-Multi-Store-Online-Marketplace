using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class OrderManager
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderItemRepository _orderItemRepository;
        private readonly IOrderStatusHistoryRepository _statusRepository;
        private readonly ICartRepository _cartRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly IStoreRepository _storeRepository;
        private readonly NotificationManager _notificationManager;

        public OrderManager(
            IOrderRepository orderRepository,
            IOrderItemRepository orderItemRepository,
            IOrderStatusHistoryRepository statusRepository,
            ICartRepository cartRepository,
            ICouponRepository couponRepository,
            IStoreRepository storeRepository,
            NotificationManager notificationManager)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _statusRepository = statusRepository;
            _cartRepository = cartRepository;
            _couponRepository = couponRepository;
            _storeRepository = storeRepository;
            _notificationManager = notificationManager;
        }

        // =========================
        // CHECKOUT
        // =========================
        public async Task<int> CheckoutAsync(
            int customerId,
            int addressId,
            string paymentMethod,
            string? couponCode = null)
        {
            var cart = await _cartRepository.GetCartByCustomerAsync(customerId);

            if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
                throw new Exception("Cart is empty");

            // =========================
            // FR-17 STOCK VALIDATION BEFORE CHECKOUT
            // =========================
            foreach (var item in cart.CartItems)
            {
                if (item.Product == null)
                    throw new Exception("Product not found in cart.");

                if (!item.Product.IsActive)
                    throw new Exception($"Product '{item.Product.ProductName}' is not available.");

                if (item.Quantity <= 0)
                    throw new Exception($"Invalid quantity for '{item.Product.ProductName}'.");

                if (item.Product.Quantity < item.Quantity)
                {
                    throw new Exception(
                        $"Not enough stock for '{item.Product.ProductName}'. Available quantity: {item.Product.Quantity}");
                }
            }

            decimal subtotal = cart.CartItems.Sum(i => i.Quantity * i.PriceAtAddTime);
            decimal discount = 0;

            // =========================
            // COUPON LOGIC
            // =========================
            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var coupon = await _couponRepository.GetByCodeAsync(couponCode);

                if (coupon != null && coupon.IsActive)
                {
                    if (coupon.StartDate <= DateTime.UtcNow &&
                        coupon.EndDate >= DateTime.UtcNow)
                    {
                        if (coupon.MinimumOrderAmount.HasValue &&
                            subtotal < coupon.MinimumOrderAmount.Value)
                        {
                            throw new Exception("Order does not meet coupon minimum amount.");
                        }

                        if (coupon.UsageLimit.HasValue &&
                            coupon.UsedCount >= coupon.UsageLimit.Value)
                        {
                            throw new Exception("Coupon usage limit reached.");
                        }

                        if (coupon.DiscountType == "Percentage")
                        {
                            discount = subtotal * (coupon.DiscountValue / 100m);

                            if (coupon.MaximumDiscountAmount.HasValue)
                            {
                                discount = Math.Min(
                                    discount,
                                    coupon.MaximumDiscountAmount.Value);
                            }
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
            // ORDER ITEMS + FR-17 + FR-18
            // =========================
            foreach (var item in cart.CartItems)
            {
                if (item.Product == null)
                    throw new Exception("Product not found in cart.");

                var orderItem = new OrderItem
                {
                    OrderID = order.OrderID,
                    ProductID = item.ProductID,
                    StoreID = item.Product.StoreID,
                    ProductName = item.Product.ProductName,
                    ProductPrice = item.PriceAtAddTime,
                    Quantity = item.Quantity,
                    TotalPrice = item.Quantity * item.PriceAtAddTime
                };

                await _orderItemRepository.AddAsync(orderItem);

                // FR-17: decrease product stock
                item.Product.Quantity -= item.Quantity;

                if (item.Product.Quantity < 0)
                    throw new Exception($"Stock error for '{item.Product.ProductName}'.");

                item.Product.UpdatedAt = DateTime.UtcNow;

                // FR-18: low stock notification
                if (item.Product.Quantity <= item.Product.LowStockThreshold)
                {
                    var store = await _storeRepository.GetByIdAsync(item.Product.StoreID);

                    if (store == null)
                        throw new Exception($"Store not found for product '{item.Product.ProductName}'.");

                    if (store.OwnerUserID <= 0)
                        throw new Exception($"Store owner not found for store '{store.StoreName}'.");

                    await _notificationManager.SendAsync(
                        userId: store.OwnerUserID,
                        title: "Low Stock Alert",
                        message: $"Product '{item.Product.ProductName}' is low in stock. Current quantity: {item.Product.Quantity}.",
                        type: "LowStock",
                        referenceId: item.Product.ProductID,
                        sentVia: "System");
                }
            }

            // Save stock changes
            await _orderRepository.UpdateAsync(order);

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
                Notes = "Checkout completed, inventory deducted, and low stock checked."
            });

            // =========================
            // CLEAR CART
            // =========================
            foreach (var item in cart.CartItems.ToList())
            {
                cart.CartItems.Remove(item);
            }

            await _orderRepository.UpdateAsync(order);

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
        public async Task UpdateOrderStatusAsync(
            int orderId,
            string newStatus,
            string changedBy)
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
        // GET CUSTOMER ORDERS
        // =========================
        public async Task<List<OrderDTO>> GetCustomerOrdersAsync(int customerId)
        {
            var orders = await _orderRepository.GetByCustomerAsync(customerId);

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
                Subtotal = o.Subtotal,
                DeliveryFee = o.DeliveryFee,
                DiscountAmount = o.DiscountAmount,
                TaxAmount = o.TaxAmount,
                TotalAmount = o.TotalAmount,
                CancellationReason = o.CancellationReason,
                CancelledAt = o.CancelledAt,
                Notes = o.Notes,
                OrderItems = o.OrderItems
            }).ToList();
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