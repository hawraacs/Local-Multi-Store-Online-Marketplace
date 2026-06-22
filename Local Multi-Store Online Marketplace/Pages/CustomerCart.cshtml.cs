using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerCartModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SubscriptionService _subscriptionService;

        private const decimal FreeDeliveryThreshold = 50m;
        private const decimal BaseDeliveryFee = 2.00m;
        private const decimal RatePerKm = 0.50m;
        private const decimal DefaultDeliveryFeePerStore = 3.00m;

        private static readonly HttpClient DistanceHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        public CustomerCartModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            SubscriptionService subscriptionService)
        {
            _context = context;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
        }

        // =====================================================
        // PAGE DATA
        // =====================================================
        public List<CustomerCartItemViewModel> CartItems { get; set; }
            = new();

        public decimal TotalAmount { get; set; }

        public decimal EstimatedDeliveryFee { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal GrandTotal { get; set; }

        public decimal FinalTotal { get; set; }

        public bool HasActiveAddress { get; set; }

        public string? CouponMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? AppliedCouponCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool CheckoutAfterAddress { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PaymentMethod { get; set; }
            = "Cash On Delivery";

        // =====================================================
        // GET CART
        // =====================================================
        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            // Continue checkout automatically after the customer
            // adds an address and returns to the cart page.
            if (CheckoutAfterAddress)
            {
                return await PlaceOrderFromCartAsync(
                    customerId.Value,
                    AppliedCouponCode,
                    PaymentMethod);
            }

            await LoadCartAsync(customerId.Value);

            return Page();
        }

        // =====================================================
        // UPDATE QUANTITY
        // =====================================================
        public async Task<IActionResult> OnPostUpdateQuantityAsync(
            int cartItemId,
            int quantity)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (quantity <= 0)
            {
                TempData["Error"] =
                    "Quantity must be greater than 0.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemID == cartItemId &&
                    ci.Cart.CustomerID == customerId.Value);

            if (cartItem == null)
            {
                TempData["Error"] =
                    "Cart item not found.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            if (cartItem.Product == null ||
                cartItem.Product.Quantity < quantity)
            {
                TempData["Error"] =
                    "Not enough stock available.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            cartItem.Quantity = quantity;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Cart updated successfully.";

            return RedirectToPage(
                new { AppliedCouponCode });
        }

        // =====================================================
        // REMOVE ITEM
        // =====================================================
        public async Task<IActionResult> OnPostRemoveAsync(
            int cartItemId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemID == cartItemId &&
                    ci.Cart.CustomerID == customerId.Value);

            if (cartItem == null)
            {
                TempData["Error"] =
                    "Cart item not found.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            _context.CartItems.Remove(cartItem);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Item removed from cart.";

            return RedirectToPage(
                new { AppliedCouponCode });
        }

        // =====================================================
        // CLEAR CART
        // =====================================================
        public async Task<IActionResult> OnPostClearAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId.Value);

            if (cart == null)
            {
                return RedirectToPage();
            }

            _context.CartItems.RemoveRange(
                cart.CartItems);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Cart cleared successfully.";

            return RedirectToPage();
        }

        // =====================================================
        // APPLY COUPON
        // =====================================================
        public async Task<IActionResult> OnPostApplyCouponAsync(
            string couponCode)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                TempData["Error"] =
                    "Please enter a coupon code.";

                return RedirectToPage();
            }

            var cleanCode =
                couponCode.Trim().ToUpperInvariant();

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId.Value);

            if (cart == null ||
                cart.CartItems == null ||
                !cart.CartItems.Any())
            {
                TempData["Error"] =
                    "Your cart is empty.";

                return RedirectToPage();
            }

            var subtotal = cart.CartItems.Sum(
                item =>
                    item.PriceAtAddTime *
                    item.Quantity);

            var result =
                await CalculateCouponDiscountAsync(
                    cleanCode,
                    cart.CartItems.ToList(),
                    subtotal);

            if (!result.IsValid)
            {
                TempData["Error"] =
                    result.Message;

                return RedirectToPage();
            }

            TempData["Success"] =
                $"Coupon {cleanCode} applied successfully. " +
                $"Discount: ${result.DiscountAmount:N2}.";

            return RedirectToPage(
                new
                {
                    AppliedCouponCode = cleanCode
                });
        }

        // =====================================================
        // REMOVE COUPON
        // =====================================================
        public IActionResult OnPostRemoveCoupon()
        {
            TempData["Success"] =
                "Coupon removed.";

            return RedirectToPage();
        }

        // =====================================================
        // CHECKOUT
        // =====================================================
        public async Task<IActionResult> OnPostCheckoutAsync(
            string? appliedCouponCode,
            string? paymentMethod)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            return await PlaceOrderFromCartAsync(
                customerId.Value,
                appliedCouponCode,
                paymentMethod);
        }

        // =====================================================
        // PLACE ORDER
        // =====================================================
        private async Task<IActionResult> PlaceOrderFromCartAsync(
            int customerId,
            string? appliedCouponCode,
            string? paymentMethod)
        {
            var cleanPaymentMethod =
                string.IsNullOrWhiteSpace(paymentMethod)
                    ? "Cash On Delivery"
                    : paymentMethod.Trim();

            if (!string.Equals(
                    cleanPaymentMethod,
                    "Cash On Delivery",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    cleanPaymentMethod,
                    "Online Payment",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "Invalid payment method.";

                return RedirectToPage(
                    new
                    {
                        AppliedCouponCode =
                            appliedCouponCode
                    });
            }

            if (string.Equals(
                    cleanPaymentMethod,
                    "Cash On Delivery",
                    StringComparison.OrdinalIgnoreCase))
            {
                cleanPaymentMethod =
                    "Cash On Delivery";
            }
            else
            {
                cleanPaymentMethod =
                    "Online Payment";
            }

            // COD remains pending until the delivery is completed.
            // Online Payment remains pending until the customer pays.
            var orderPaymentStatus = "Pending";

            var paymentGateway =
                cleanPaymentMethod == "Online Payment"
                    ? "Simulated Gateway"
                    : "Cash";

            var paymentRecordStatus = "Pending";

            var customer = await _context.Customers
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId);

            if (customer == null)
            {
                TempData["Error"] =
                    "Customer profile was not found.";

                return RedirectToPage();
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId);

            if (cart == null ||
                cart.CartItems == null ||
                !cart.CartItems.Any())
            {
                TempData["Error"] =
                    "Your cart is empty.";

                return RedirectToPage();
            }

            var address = customer.Addresses
                .FirstOrDefault(addressItem =>
                    addressItem.IsDefault &&
                    addressItem.IsActive)
                ?? customer.Addresses
                    .FirstOrDefault(addressItem =>
                        addressItem.IsActive);

            if (address == null)
            {
                TempData["Error"] =
                    "Please add an active delivery address " +
                    "before checkout.";

                var encodedCoupon =
                    Uri.EscapeDataString(
                        appliedCouponCode ??
                        string.Empty);

                var encodedPayment =
                    Uri.EscapeDataString(
                        cleanPaymentMethod);

                return RedirectToPage(
                    "/CustomerAddresses",
                    new
                    {
                        returnUrl =
                            $"/CustomerCart" +
                            $"?CheckoutAfterAddress=true" +
                            $"&AppliedCouponCode={encodedCoupon}" +
                            $"&PaymentMethod={encodedPayment}"
                    });
            }

            // =================================================
            // VALIDATE PRODUCTS, SUBSCRIPTIONS, AND STOCK
            // =================================================
            foreach (var item in cart.CartItems)
            {
                if (item.Product == null ||
                    !item.Product.IsActive)
                {
                    TempData["Error"] =
                        "One of the products is no longer available.";

                    return RedirectToPage(
                        new
                        {
                            AppliedCouponCode =
                                appliedCouponCode
                        });
                }

                if (!_subscriptionService.CanReceiveOrders(
                        item.Product.StoreID))
                {
                    var store = await _context.Stores
                        .FirstOrDefaultAsync(storeItem =>
                            storeItem.StoreID ==
                            item.Product.StoreID);

                    TempData["Error"] =
                        $"Store '{store?.StoreName}' subscription " +
                        $"has expired and cannot receive orders.";

                    return RedirectToPage(
                        new
                        {
                            AppliedCouponCode =
                                appliedCouponCode
                        });
                }

                if (item.Product.Quantity <
                    item.Quantity)
                {
                    TempData["Error"] =
                        $"Not enough stock available for " +
                        $"{item.Product.ProductName}.";

                    return RedirectToPage(
                        new
                        {
                            AppliedCouponCode =
                                appliedCouponCode
                        });
                }
            }

            var subtotal = cart.CartItems.Sum(
                item =>
                    item.PriceAtAddTime *
                    item.Quantity);

            var deliveryFee =
                await CalculateDeliveryFeeAsync(
                    cart.CartItems.ToList(),
                    address,
                    subtotal);

            var couponResult =
                await CalculateCouponDiscountAsync(
                    appliedCouponCode,
                    cart.CartItems.ToList(),
                    subtotal);

            if (!couponResult.IsValid &&
                !string.IsNullOrWhiteSpace(
                    appliedCouponCode))
            {
                TempData["Error"] =
                    couponResult.Message;

                return RedirectToPage(
                    new
                    {
                        AppliedCouponCode =
                            appliedCouponCode
                    });
            }

            var discountAmount =
                couponResult.DiscountAmount;

            var taxAmount = 0m;

            var totalAmount =
                subtotal +
                deliveryFee +
                taxAmount -
                discountAmount;

            if (totalAmount < 0)
            {
                totalAmount = 0;
            }

            // =================================================
            // CREATE ORDER
            // =================================================
            var order = new Order
            {
                OrderNumber =
                    $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-" +
                    $"{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}",

                CustomerID =
                    customerId,

                AddressID =
                    address.AddressID,

                OrderDate =
                    DateTime.UtcNow,

                Status =
                    "Pending",

                PaymentMethod =
                    cleanPaymentMethod,

                PaymentStatus =
                    orderPaymentStatus,

                Subtotal =
                    subtotal,

                DeliveryFee =
                    deliveryFee,

                DiscountAmount =
                    discountAmount,

                TaxAmount =
                    taxAmount,

                TotalAmount =
                    totalAmount
            };

            _context.Orders.Add(order);

            await _context.SaveChangesAsync();

            // =================================================
            // CREATE PAYMENT RECORD
            // =================================================
            var payment = new Payment
            {
                OrderID =
                    order.OrderID,

                PaymentMethod =
                    cleanPaymentMethod,

                PaymentGateway =
                    paymentGateway,

                GatewayTransactionID =
                    null,

                Amount =
                    totalAmount,

                PaymentDate =
                    DateTime.UtcNow,

                Status =
                    paymentRecordStatus
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            // =================================================
            // UPDATE COUPON USAGE
            // =================================================
            if (couponResult.Coupon != null &&
                discountAmount > 0)
            {
                couponResult.Coupon.UsedCount += 1;

                await _context.SaveChangesAsync();
            }

            // =================================================
            // CREATE ORDER ITEMS AND UPDATE STOCK
            // =================================================
            foreach (var item in cart.CartItems.ToList())
            {
                if (item.Product == null)
                {
                    TempData["Error"] =
                        "One of the products is no longer available.";

                    return RedirectToPage(
                        new
                        {
                            AppliedCouponCode =
                                appliedCouponCode
                        });
                }

                var orderItem = new OrderItem
                {
                    OrderID =
                        order.OrderID,

                    ProductID =
                        item.ProductID,

                    StoreID =
                        item.Product.StoreID,

                    ProductName =
                        item.Product.ProductName,

                    ProductPrice =
                        item.PriceAtAddTime,

                    Quantity =
                        item.Quantity,

                    TotalPrice =
                        item.PriceAtAddTime *
                        item.Quantity
                };

                _context.OrderItems.Add(orderItem);

                item.Product.Quantity -= item.Quantity;

                if (item.Product.Quantity < 0)
                {
                    throw new InvalidOperationException(
                        $"Stock error for " +
                        $"'{item.Product.ProductName}'.");
                }

                item.Product.UpdatedAt =
                    DateTime.UtcNow;

                // =============================================
                // LOW-STOCK NOTIFICATION
                // =============================================
                if (item.Product.Quantity <=
                    item.Product.LowStockThreshold)
                {
                    var store = await _context.Stores
                        .FirstOrDefaultAsync(storeItem =>
                            storeItem.StoreID ==
                            item.Product.StoreID);

                    if (store != null)
                    {
                        _context.Notifications.Add(
                            new Notification
                            {
                                UserID =
                                    store.OwnerUserID,

                                Title =
                                    "Low Stock Alert",

                                Message =
                                    $"Product " +
                                    $"'{item.Product.ProductName}' " +
                                    $"is low in stock. " +
                                    $"Current quantity: " +
                                    $"{item.Product.Quantity}.",

                                Type =
                                    "LowStock",

                                ReferenceID =
                                    item.Product.ProductID,

                                IsRead =
                                    false,

                                SentAt =
                                    DateTime.UtcNow,

                                SentVia =
                                    "System"
                            });
                    }
                }
            }

            // =================================================
            // CLEAR CART AFTER ORDER CREATION
            // =================================================
            _context.CartItems.RemoveRange(
                cart.CartItems);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // =================================================
            // ONLINE PAYMENT FLOW
            // =================================================
            if (cleanPaymentMethod ==
                "Online Payment")
            {
                TempData["Success"] =
                    $"Order created successfully. " +
                    $"Please complete your online payment. " +
                    $"Total: ${totalAmount:N2}.";

                return RedirectToPage(
                    "/OnlinePayment",
                    new
                    {
                        orderId =
                            order.OrderID
                    });
            }

            // =================================================
            // CASH ON DELIVERY AUTO-ASSIGNMENT
            //
            // customer.UserID is the original customer user ID.
            // It is used to prevent self-delivery.
            // =================================================
            var deliveryAssigned =
                await TryAutoAssignDeliveryAndNotifyAsync(
                    order,
                    address,
                    customer.UserID);

            await _context.SaveChangesAsync();

            if (deliveryAssigned)
            {
                TempData["Success"] =
                    $"Order placed successfully. " +
                    $"Payment: Cash On Delivery (Pending). " +
                    $"Delivery fee: ${deliveryFee:N2}. " +
                    $"Discount: ${discountAmount:N2}. " +
                    $"Online delivery staff has been assigned.";
            }
            else
            {
                TempData["Success"] =
                    $"Order placed successfully. " +
                    $"Payment: Cash On Delivery (Pending). " +
                    $"Delivery fee: ${deliveryFee:N2}. " +
                    $"Discount: ${discountAmount:N2}. " +
                    $"Delivery assignment is pending because " +
                    $"no online delivery staff is available.";
            }

            return RedirectToPage(
                "/CustomerOrders");
        }

        // =====================================================
        // AUTO-ASSIGN DELIVERY AND SEND NOTIFICATIONS
        // =====================================================
        private async Task<bool>
            TryAutoAssignDeliveryAndNotifyAsync(
                Order order,
                CustomerAddress customerAddress,
                int customerUserId)
        {
            var alreadyAssigned =
                await _context.DeliveryAssignments
                    .AnyAsync(assignment =>
                        assignment.OrderID ==
                            order.OrderID &&
                        assignment.Status !=
                            "Delivered" &&
                        assignment.Status !=
                            "Cancelled" &&
                        assignment.Status !=
                            "Failed");

            if (alreadyAssigned)
            {
                return true;
            }

            var customerArea =
                customerAddress.Area?
                    .Trim()
                    .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(
                    customerArea))
            {
                order.Status = "Pending";

                await NotifyAdminsAsync(
                    "Delivery Assignment Needed",
                    $"Order {order.OrderNumber} was placed, " +
                    $"but the customer area is missing.",
                    "DeliveryAssignmentPending",
                    order.OrderID);

                return false;
            }

            // Delivery person is considered online when the
            // location was updated within the last five minutes.
            var onlineLimit =
                DateTime.UtcNow.AddMinutes(-5);

            var onlineSameAreaDeliveryPeople =
                await _context.DeliveryPersons
                    .Where(delivery =>
                        delivery.IsActive &&
                        delivery.Status == "Approved" &&

                        // Prevent a known original customer from
                        // delivering their own order.
                        //
                        // Old records with NULL RequestedByUserID
                        // remain eligible, matching Admin Assign.
                        (
                            !delivery.RequestedByUserID.HasValue ||
                            delivery.RequestedByUserID.Value !=
                                customerUserId
                        ) &&

                        delivery.LastLocationUpdate.HasValue &&
                        delivery.LastLocationUpdate.Value >=
                            onlineLimit &&

                        delivery.CurrentLatitude.HasValue &&
                        delivery.CurrentLongitude.HasValue &&

                        !string.IsNullOrWhiteSpace(
                            delivery.Area) &&

                        delivery.Area
                            .Trim()
                            .ToLower() ==
                            customerArea)
                    .ToListAsync();

            if (!onlineSameAreaDeliveryPeople.Any())
            {
                order.Status = "Pending";

                await NotifyAdminsAsync(
                    "Delivery Assignment Needed",
                    $"Order {order.OrderNumber} was placed " +
                    $"for area {customerAddress.Area}, but " +
                    $"no online delivery staff is available " +
                    $"in that area.",
                    "DeliveryAssignmentPending",
                    order.OrderID);

                return false;
            }

            DeliveryPerson selectedDeliveryPerson;

            if (customerAddress.Latitude.HasValue &&
                customerAddress.Longitude.HasValue)
            {
                selectedDeliveryPerson =
                    onlineSameAreaDeliveryPeople
                        .OrderBy(delivery =>
                            CalculateDistanceKm(
                                Convert.ToDouble(
                                    delivery
                                        .CurrentLatitude!
                                        .Value),

                                Convert.ToDouble(
                                    delivery
                                        .CurrentLongitude!
                                        .Value),

                                customerAddress
                                    .Latitude
                                    .Value,

                                customerAddress
                                    .Longitude
                                    .Value))
                        .ThenByDescending(delivery =>
                            delivery.Rating)
                        .ThenBy(delivery =>
                            delivery.DeliveryPersonID)
                        .First();
            }
            else
            {
                selectedDeliveryPerson =
                    onlineSameAreaDeliveryPeople
                        .OrderByDescending(delivery =>
                            delivery.Rating)
                        .ThenBy(delivery =>
                            delivery.DeliveryPersonID)
                        .First();
            }

            var assignment =
                new DeliveryAssignment
                {
                    OrderID =
                        order.OrderID,

                    DeliveryPersonID =
                        selectedDeliveryPerson
                            .DeliveryPersonID,

                    AssignedAt =
                        DateTime.UtcNow,

                    PickupTime =
                        null,

                    DeliveryTime =
                        null,

                    Status =
                        "Assigned",

                    DeliveryProofImageURL =
                        null
                };

            _context.DeliveryAssignments.Add(
                assignment);

            // Important:
            // Assignment does not mean delivery has started.
            // The driver must click Start Delivery first.
            order.Status = "Assigned";

            // =================================================
            // DELIVERY NOTIFICATION
            // =================================================
            _context.Notifications.Add(
                new Notification
                {
                    UserID =
                        selectedDeliveryPerson.UserID,

                    Title =
                        "New Delivery Assigned",

                    Message =
                        $"You have been assigned to deliver " +
                        $"order {order.OrderNumber}. " +
                        $"Customer area: " +
                        $"{customerAddress.Area}.",

                    Type =
                        "DeliveryAssignment",

                    ReferenceID =
                        order.OrderID,

                    IsRead =
                        false,

                    SentAt =
                        DateTime.UtcNow,

                    SentVia =
                        "System"
                });

            // =================================================
            // ADMIN NOTIFICATION
            // =================================================
            await NotifyAdminsAsync(
                "Delivery Assigned Automatically",
                $"Order {order.OrderNumber} was assigned " +
                $"to {selectedDeliveryPerson.FullName} " +
                $"for area {customerAddress.Area}.",
                "DeliveryAssigned",
                order.OrderID);

            return true;
        }

        // =====================================================
        // STRAIGHT-LINE DISTANCE
        // =====================================================
        private static double CalculateDistanceKm(
            double lat1,
            double lon1,
            double lat2,
            double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat =
                DegreesToRadians(lat2 - lat1);

            var dLon =
                DegreesToRadians(lon2 - lon1);

            var value =
                Math.Sin(dLat / 2) *
                Math.Sin(dLat / 2) +

                Math.Cos(
                    DegreesToRadians(lat1)) *

                Math.Cos(
                    DegreesToRadians(lat2)) *

                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            var centralAngle =
                2 * Math.Atan2(
                    Math.Sqrt(value),
                    Math.Sqrt(1 - value));

            return earthRadiusKm *
                   centralAngle;
        }

        private static double DegreesToRadians(
            double degrees)
        {
            return degrees *
                   Math.PI /
                   180;
        }

        // =====================================================
        // NOTIFY ADMINS
        // =====================================================
        private async Task NotifyAdminsAsync(
            string title,
            string message,
            string type,
            int referenceId)
        {
            var admins =
                await _userManager
                    .GetUsersInRoleAsync("Admin");

            foreach (var admin in admins)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        UserID =
                            admin.Id,

                        Title =
                            title,

                        Message =
                            message,

                        Type =
                            type,

                        ReferenceID =
                            referenceId,

                        IsRead =
                            false,

                        SentAt =
                            DateTime.UtcNow,

                        SentVia =
                            "System"
                    });
            }
        }

        // =====================================================
        // CALCULATE DELIVERY FEE
        // =====================================================
        private async Task<decimal>
            CalculateDeliveryFeeAsync(
                List<CartItem> cartItems,
                CustomerAddress customerAddress,
                decimal subtotal)
        {
            if (subtotal >
                FreeDeliveryThreshold)
            {
                return 0m;
            }

            var storeIds = cartItems
                .Where(item =>
                    item.Product != null)
                .Select(item =>
                    item.Product.StoreID)
                .Distinct()
                .ToList();

            if (!storeIds.Any())
            {
                return DefaultDeliveryFeePerStore;
            }

            var stores =
                await _context.Stores
                    .Where(store =>
                        storeIds.Contains(
                            store.StoreID))
                    .ToListAsync();

            var totalDeliveryFee = 0m;

            foreach (var store in stores)
            {
                if (store.HasFixedDeliveryFee &&
                    store.FixedDeliveryFee.HasValue)
                {
                    totalDeliveryFee +=
                        store.FixedDeliveryFee.Value;

                    continue;
                }

                if (store.Latitude == 0 ||
                    store.Longitude == 0 ||
                    !customerAddress.Latitude.HasValue ||
                    !customerAddress.Longitude.HasValue)
                {
                    totalDeliveryFee +=
                        DefaultDeliveryFeePerStore;

                    continue;
                }

                var distanceKm =
                    await TryGetDrivingDistanceKmAsync(
                        Convert.ToDouble(
                            store.Latitude),

                        Convert.ToDouble(
                            store.Longitude),

                        customerAddress
                            .Latitude
                            .Value,

                        customerAddress
                            .Longitude
                            .Value);

                if (distanceKm == null ||
                    distanceKm <= 0)
                {
                    totalDeliveryFee +=
                        DefaultDeliveryFeePerStore;

                    continue;
                }

                var storeDeliveryFee =
                    BaseDeliveryFee +
                    RatePerKm *
                    (decimal)distanceKm.Value;

                totalDeliveryFee +=
                    Math.Round(
                        storeDeliveryFee,
                        2);
            }

            if (totalDeliveryFee < 0)
            {
                return DefaultDeliveryFeePerStore;
            }

            return Math.Round(
                totalDeliveryFee,
                2);
        }

        // =====================================================
        // GET DRIVING DISTANCE FROM OSRM
        // =====================================================
        private async Task<double?>
            TryGetDrivingDistanceKmAsync(
                double storeLat,
                double storeLng,
                double customerLat,
                double customerLng)
        {
            try
            {
                var url =
                    $"https://router.project-osrm.org/" +
                    $"route/v1/driving/" +
                    $"{storeLng},{storeLat};" +
                    $"{customerLng},{customerLat}" +
                    $"?overview=false";

                using var response =
                    await DistanceHttpClient
                        .GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream =
                    await response.Content
                        .ReadAsStreamAsync();

                using var json =
                    await JsonDocument
                        .ParseAsync(stream);

                if (!json.RootElement
                    .TryGetProperty(
                        "routes",
                        out var routes))
                {
                    return null;
                }

                if (routes.GetArrayLength() == 0)
                {
                    return null;
                }

                var distanceMeters =
                    routes[0]
                        .GetProperty("distance")
                        .GetDouble();

                return distanceMeters /
                       1000.0;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================
        // CALCULATE COUPON DISCOUNT
        // =====================================================
        private async Task<CouponCalculationResult>
            CalculateCouponDiscountAsync(
                string? couponCode,
                List<CartItem> cartItems,
                decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(
                    couponCode))
            {
                return new CouponCalculationResult
                {
                    IsValid = true,
                    DiscountAmount = 0,
                    Message = string.Empty
                };
            }

            var cleanCode =
                couponCode
                    .Trim()
                    .ToUpperInvariant();

            var coupon =
                await _context.Coupons
                    .FirstOrDefaultAsync(couponItem =>
                        couponItem.CouponCode
                            .ToUpper() ==
                        cleanCode);

            if (coupon == null)
            {
                return new CouponCalculationResult
                {
                    IsValid = false,
                    DiscountAmount = 0,
                    Message =
                        "Invalid coupon code."
                };
            }

            if (!coupon.IsActive)
            {
                return new CouponCalculationResult
                {
                    IsValid = false,
                    DiscountAmount = 0,
                    Message =
                        "This coupon is not active."
                };
            }

            var currentDate = DateTime.UtcNow.Date; var couponStartDate = coupon.StartDate.Date; var couponEndDate = coupon.EndDate.Date; if (couponStartDate > currentDate) { return new CouponCalculationResult { IsValid = false, DiscountAmount = 0, Message = "This coupon is not active yet." }; }
            if (couponEndDate < currentDate) { return new CouponCalculationResult { IsValid = false, DiscountAmount = 0, Message = "This coupon has expired." }; }

            if (coupon.UsageLimit.HasValue &&
                coupon.UsedCount >=
                    coupon.UsageLimit.Value)
            {
                return new CouponCalculationResult
                {
                    IsValid = false,
                    DiscountAmount = 0,
                    Message =
                        "Coupon usage limit reached."
                };
            }

            decimal eligibleSubtotal =
                subtotal;

            if (coupon.StoreID.HasValue)
            {
                eligibleSubtotal =
                    cartItems
                        .Where(item =>
                            item.Product != null &&
                            item.Product.StoreID ==
                                coupon.StoreID.Value)
                        .Sum(item =>
                            item.PriceAtAddTime *
                            item.Quantity);

                if (eligibleSubtotal <= 0)
                {
                    return new CouponCalculationResult
                    {
                        IsValid = false,
                        DiscountAmount = 0,
                        Message =
                            "This coupon is not valid for " +
                            "the products in your cart."
                    };
                }
            }

            var minimumOrderAmount =
                coupon.MinimumOrderAmount ?? 0;

            if (eligibleSubtotal <
                minimumOrderAmount)
            {
                return new CouponCalculationResult
                {
                    IsValid = false,
                    DiscountAmount = 0,
                    Message =
                        $"Minimum order amount for this " +
                        $"coupon is " +
                        $"${minimumOrderAmount:N2}."
                };
            }

            decimal discountAmount;

            if (coupon.DiscountType.Equals(
                    "Percentage",
                    StringComparison.OrdinalIgnoreCase))
            {
                discountAmount =
                    eligibleSubtotal *
                    coupon.DiscountValue /
                    100m;
            }
            else if (coupon.DiscountType.Equals(
                         "Fixed",
                         StringComparison.OrdinalIgnoreCase))
            {
                discountAmount =
                    coupon.DiscountValue;
            }
            else
            {
                return new CouponCalculationResult
                {
                    IsValid = false,
                    DiscountAmount = 0,
                    Message =
                        "Invalid coupon discount type."
                };
            }

            if (coupon.MaximumDiscountAmount.HasValue &&
                coupon.MaximumDiscountAmount.Value > 0 &&
                discountAmount >
                    coupon.MaximumDiscountAmount.Value)
            {
                discountAmount =
                    coupon.MaximumDiscountAmount.Value;
            }

            if (discountAmount >
                eligibleSubtotal)
            {
                discountAmount =
                    eligibleSubtotal;
            }

            discountAmount =
                Math.Round(
                    discountAmount,
                    2);

            return new CouponCalculationResult
            {
                IsValid = true,
                DiscountAmount =
                    discountAmount,

                Message =
                    "Coupon applied successfully.",

                Coupon =
                    coupon
            };
        }

        // =====================================================
        // LOAD CART FOR DISPLAY
        // =====================================================
        private async Task LoadCartAsync(
            int customerId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Images)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Store)
                .FirstOrDefaultAsync(c =>
                    c.CustomerID == customerId);

            if (cart == null ||
                cart.CartItems == null ||
                !cart.CartItems.Any())
            {
                CartItems =
                    new List<CustomerCartItemViewModel>();

                TotalAmount = 0;
                EstimatedDeliveryFee = 0;
                DiscountAmount = 0;
                GrandTotal = 0;
                FinalTotal = 0;
                HasActiveAddress = false;
                CouponMessage = null;

                return;
            }

            CartItems = cart.CartItems
                .OrderByDescending(item =>
                    item.AddedAt)
                .Select(item =>
                    new CustomerCartItemViewModel
                    {
                        CartItemID =
                            item.CartItemID,

                        ProductID =
                            item.ProductID,

                        ProductName =
                            item.Product != null
                                ? item.Product.ProductName
                                : "Unknown Product",

                        StoreName =
                            item.Product != null &&
                            item.Product.Store != null
                                ? item.Product.Store.StoreName
                                : "Unknown Store",

                        Quantity =
                            item.Quantity,

                        UnitPrice =
                            item.PriceAtAddTime,

                        TotalPrice =
                            item.PriceAtAddTime *
                            item.Quantity,

                        AvailableStock =
                            item.Product != null
                                ? item.Product.Quantity
                                : 0,

                        ImageUrl =
                            item.Product != null
                                ? item.Product.Images
                                    .OrderByDescending(image =>
                                        image.IsPrimary)
                                    .ThenBy(image =>
                                        image.DisplayOrder)
                                    .Select(image =>
                                        image.ImageUrl)
                                    .FirstOrDefault()
                                    ?? "/images/no-image.png"
                                : "/images/no-image.png"
                    })
                .ToList();

            TotalAmount =
                CartItems.Sum(item =>
                    item.TotalPrice);

            var customer =
                await _context.Customers
                    .Include(customerItem =>
                        customerItem.Addresses)
                    .FirstOrDefaultAsync(customerItem =>
                        customerItem.CustomerID ==
                        customerId);

            var address = customer?.Addresses
                .FirstOrDefault(addressItem =>
                    addressItem.IsDefault &&
                    addressItem.IsActive)
                ?? customer?.Addresses
                    .FirstOrDefault(addressItem =>
                        addressItem.IsActive);

            HasActiveAddress =
                address != null;

            if (address != null)
            {
                EstimatedDeliveryFee =
                    await CalculateDeliveryFeeAsync(
                        cart.CartItems.ToList(),
                        address,
                        TotalAmount);
            }
            else
            {
                EstimatedDeliveryFee = 0m;
            }

            var couponResult =
                await CalculateCouponDiscountAsync(
                    AppliedCouponCode,
                    cart.CartItems.ToList(),
                    TotalAmount);

            if (!string.IsNullOrWhiteSpace(
                    AppliedCouponCode))
            {
                if (couponResult.IsValid)
                {
                    DiscountAmount =
                        couponResult.DiscountAmount;

                    CouponMessage =
                        couponResult.Message;
                }
                else
                {
                    DiscountAmount = 0;

                    CouponMessage =
                        couponResult.Message;
                }
            }
            else
            {
                DiscountAmount = 0;
                CouponMessage = null;
            }

            GrandTotal =
                TotalAmount +
                EstimatedDeliveryFee -
                DiscountAmount;

            if (GrandTotal < 0)
            {
                GrandTotal = 0;
            }

            FinalTotal =
                GrandTotal;
        }

        // =====================================================
        // GET CURRENT CUSTOMER ID
        // =====================================================
        private async Task<int?>
            GetCurrentCustomerIdAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            var customer =
                await _context.Customers
                    .FirstOrDefaultAsync(customerItem =>
                        customerItem.UserID ==
                        user.Id);

            return customer?.CustomerID;
        }
    }

    // =========================================================
    // CART ITEM VIEW MODEL
    // =========================================================
    public class CustomerCartItemViewModel
    {
        public int CartItemID { get; set; }

        public int ProductID { get; set; }

        public string ProductName { get; set; }
            = string.Empty;

        public string StoreName { get; set; }
            = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public int AvailableStock { get; set; }

        public string ImageUrl { get; set; }
            = "/images/no-image.png";
    }

    // =========================================================
    // COUPON CALCULATION RESULT
    // =========================================================
    public class CouponCalculationResult
    {
        public bool IsValid { get; set; }

        public decimal DiscountAmount { get; set; }

        public string Message { get; set; }
            = string.Empty;

        public Coupon? Coupon { get; set; }
    }
}
