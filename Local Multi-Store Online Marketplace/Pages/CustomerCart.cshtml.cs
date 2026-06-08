using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.Text.Json;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerCartModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

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
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<CustomerCartItemViewModel> CartItems { get; set; } = new();

        public decimal TotalAmount { get; set; }

        public decimal EstimatedDeliveryFee { get; set; }

        public decimal GrandTotal { get; set; }

        public bool HasActiveAddress { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool CheckoutAfterAddress { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (CheckoutAfterAddress)
            {
                return await PlaceOrderFromCartAsync(customerId.Value);
            }

            await LoadCartAsync(customerId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int cartItemId, int quantity)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                return RedirectToPage();
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemID == cartItemId &&
                    ci.Cart.CustomerID == customerId.Value);

            if (cartItem == null)
            {
                TempData["Error"] = "Cart item not found.";
                return RedirectToPage();
            }

            if (cartItem.Product == null || cartItem.Product.Quantity < quantity)
            {
                TempData["Error"] = "Not enough stock available.";
                return RedirectToPage();
            }

            cartItem.Quantity = quantity;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cart updated successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveAsync(int cartItemId)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemID == cartItemId &&
                    ci.Cart.CustomerID == customerId.Value);

            if (cartItem == null)
            {
                TempData["Error"] = "Cart item not found.";
                return RedirectToPage();
            }

            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            _context.CartItems.Remove(cartItem);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Item removed from cart.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId.Value);

            if (cart == null)
            {
                return RedirectToPage();
            }

            _context.CartItems.RemoveRange(cart.CartItems);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cart cleared successfully.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCheckoutAsync()
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                TempData["Error"] = "Please login as a customer first.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return await PlaceOrderFromCartAsync(customerId.Value);
        }

        private async Task<IActionResult> PlaceOrderFromCartAsync(int customerId)
        {
            var customer = await _context.Customers
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile was not found.";
                return RedirectToPage();
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToPage();
            }

            var address = customer.Addresses
                .FirstOrDefault(a => a.IsDefault && a.IsActive)
                ?? customer.Addresses.FirstOrDefault(a => a.IsActive);

            if (address == null)
            {
                TempData["Error"] = "Please add an active delivery address before checkout.";

                return RedirectToPage("/CustomerAddresses", new
                {
                    returnUrl = "/CustomerCart?CheckoutAfterAddress=true"
                });
            }

            foreach (var item in cart.CartItems)
            {
                if (item.Product == null || !item.Product.IsActive)
                {
                    TempData["Error"] = "One of the products is no longer available.";
                    return RedirectToPage();
                }

                if (item.Product.Quantity < item.Quantity)
                {
                    TempData["Error"] = $"Not enough stock available for {item.Product.ProductName}.";
                    return RedirectToPage();
                }
            }

            var subtotal = cart.CartItems.Sum(i => i.PriceAtAddTime * i.Quantity);

            var deliveryFee = await CalculateDeliveryFeeAsync(
                cart.CartItems.ToList(),
                address,
                subtotal);

            var discountAmount = 0m;
            var taxAmount = 0m;
            var totalAmount = subtotal + deliveryFee + taxAmount - discountAmount;

            var order = new Order
            {
                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                CustomerID = customerId,
                AddressID = address.AddressID,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                PaymentMethod = "Cash On Delivery",
                PaymentStatus = "Unpaid",
                Subtotal = subtotal,
                DeliveryFee = deliveryFee,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                TotalAmount = totalAmount
            };

            _context.Orders.Add(order);

            await _context.SaveChangesAsync();

            foreach (var item in cart.CartItems.ToList())
            {
                if (item.Product == null)
                {
                    TempData["Error"] = "One of the products is no longer available.";
                    return RedirectToPage();
                }

                var orderItem = new OrderItem
                {
                    OrderID = order.OrderID,
                    ProductID = item.ProductID,
                    StoreID = item.Product.StoreID,
                    ProductName = item.Product.ProductName,
                    ProductPrice = item.PriceAtAddTime,
                    Quantity = item.Quantity,
                    TotalPrice = item.PriceAtAddTime * item.Quantity
                };

                _context.OrderItems.Add(orderItem);

                item.Product.Quantity -= item.Quantity;

                if (item.Product.Quantity < 0)
                {
                    throw new Exception($"Stock error for '{item.Product.ProductName}'.");
                }

                item.Product.UpdatedAt = DateTime.UtcNow;

                if (item.Product.Quantity <= item.Product.LowStockThreshold)
                {
                    var store = await _context.Stores
                        .FirstOrDefaultAsync(s => s.StoreID == item.Product.StoreID);

                    if (store != null)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserID = store.OwnerUserID,
                            Title = "Low Stock Alert",
                            Message = $"Product '{item.Product.ProductName}' is low in stock. Current quantity: {item.Product.Quantity}.",
                            Type = "LowStock",
                            ReferenceID = item.Product.ProductID,
                            IsRead = false,
                            SentAt = DateTime.UtcNow,
                            SentVia = "System"
                        });
                    }
                }
            }

            var deliveryAssigned = await TryAutoAssignDeliveryAndNotifyAsync(order, address);

            _context.CartItems.RemoveRange(cart.CartItems);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (deliveryAssigned)
            {
                TempData["Success"] =
                    $"Order placed successfully. Delivery fee: ${deliveryFee:N2}. Online delivery staff has been assigned.";
            }
            else
            {
                TempData["Success"] =
                    $"Order placed successfully. Delivery fee: ${deliveryFee:N2}. Delivery assignment is pending because no online delivery staff is available.";
            }

            return RedirectToPage("/CustomerOrders");
        }

        private async Task<bool> TryAutoAssignDeliveryAndNotifyAsync(
    Order order,
    CustomerAddress customerAddress)
        {
            var alreadyAssigned = await _context.DeliveryAssignments
                .AnyAsync(a =>
                    a.OrderID == order.OrderID &&
                    a.Status != "Delivered" &&
                    a.Status != "Cancelled" &&
                    a.Status != "Failed");

            if (alreadyAssigned)
            {
                return true;
            }

            var customerArea = customerAddress.Area?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(customerArea))
            {
                order.Status = "Pending";

                await NotifyAdminsAsync(
                    "Delivery Assignment Needed",
                    $"Order {order.OrderNumber} was placed, but the customer area is missing.",
                    "DeliveryAssignmentPending",
                    order.OrderID);

                return false;
            }

            var onlineLimit = DateTime.UtcNow.AddMinutes(-5);

            var onlineSameAreaDeliveryPeople = await _context.DeliveryPersons
                .Where(d =>
                    d.IsActive &&
                    d.Status == "Approved" &&
                    d.LastLocationUpdate.HasValue &&
                    d.LastLocationUpdate.Value >= onlineLimit &&
                    d.CurrentLatitude.HasValue &&
                    d.CurrentLongitude.HasValue &&
                    !string.IsNullOrWhiteSpace(d.Area) &&
                    d.Area.Trim().ToLower() == customerArea)
                .ToListAsync();

            if (!onlineSameAreaDeliveryPeople.Any())
            {
                order.Status = "Pending";

                await NotifyAdminsAsync(
                    "Delivery Assignment Needed",
                    $"Order {order.OrderNumber} was placed for area {customerAddress.Area}, but no online delivery staff is available in that area.",
                    "DeliveryAssignmentPending",
                    order.OrderID);

                return false;
            }

            DeliveryPerson selectedDeliveryPerson;

            if (customerAddress.Latitude.HasValue &&
                customerAddress.Longitude.HasValue)
            {
                selectedDeliveryPerson = onlineSameAreaDeliveryPeople
                    .OrderBy(d => CalculateDistanceKm(
                        Convert.ToDouble(d.CurrentLatitude!.Value),
                        Convert.ToDouble(d.CurrentLongitude!.Value),
                        customerAddress.Latitude.Value,
                        customerAddress.Longitude.Value))
                    .ThenByDescending(d => d.Rating)
                    .ThenBy(d => d.DeliveryPersonID)
                    .First();
            }
            else
            {
                selectedDeliveryPerson = onlineSameAreaDeliveryPeople
                    .OrderByDescending(d => d.Rating)
                    .ThenBy(d => d.DeliveryPersonID)
                    .First();
            }

            var assignment = new DeliveryAssignment
            {
                OrderID = order.OrderID,
                DeliveryPersonID = selectedDeliveryPerson.DeliveryPersonID,
                AssignedAt = DateTime.UtcNow,
                Status = "Assigned",
                DeliveryProofImageURL = null
            };

            _context.DeliveryAssignments.Add(assignment);

            order.Status = "Out for Delivery";

            _context.Notifications.Add(new Notification
            {
                UserID = selectedDeliveryPerson.UserID,
                Title = "New Delivery Assigned",
                Message = $"You have been assigned to deliver order {order.OrderNumber}. Customer area: {customerAddress.Area}.",
                Type = "DeliveryAssignment",
                ReferenceID = order.OrderID,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = "System"
            });

            await NotifyAdminsAsync(
                "Delivery Assigned Automatically",
                $"Order {order.OrderNumber} was assigned to {selectedDeliveryPerson.FullName} for area {customerAddress.Area}.",
                "DeliveryAssigned",
                order.OrderID);

            return true;
        }

        private static double CalculateDistanceKm(
            double lat1,
            double lon1,
            double lat2,
            double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private async Task NotifyAdminsAsync(
            string title,
            string message,
            string type,
            int referenceId)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            foreach (var admin in admins)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = admin.Id,
                    Title = title,
                    Message = message,
                    Type = type,
                    ReferenceID = referenceId,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });
            }
        }

        private async Task<decimal> CalculateDeliveryFeeAsync(
            List<CartItem> cartItems,
            CustomerAddress customerAddress,
            decimal subtotal)
        {
            if (subtotal > FreeDeliveryThreshold)
            {
                return 0m;
            }

            var storeIds = cartItems
                .Where(i => i.Product != null)
                .Select(i => i.Product.StoreID)
                .Distinct()
                .ToList();

            if (!storeIds.Any())
            {
                return DefaultDeliveryFeePerStore;
            }

            var stores = await _context.Stores
                .Where(s => storeIds.Contains(s.StoreID))
                .ToListAsync();

            var totalDeliveryFee = 0m;

            foreach (var store in stores)
            {
                if (store.HasFixedDeliveryFee && store.FixedDeliveryFee.HasValue)
                {
                    totalDeliveryFee += store.FixedDeliveryFee.Value;
                    continue;
                }

                if (store.Latitude == 0 ||
                    store.Longitude == 0 ||
                    !customerAddress.Latitude.HasValue ||
                    !customerAddress.Longitude.HasValue)
                {
                    totalDeliveryFee += DefaultDeliveryFeePerStore;
                    continue;
                }

                var distanceKm = await TryGetDrivingDistanceKmAsync(
                    Convert.ToDouble(store.Latitude),
                    Convert.ToDouble(store.Longitude),
                    customerAddress.Latitude.Value,
                    customerAddress.Longitude.Value);

                if (distanceKm == null || distanceKm <= 0)
                {
                    totalDeliveryFee += DefaultDeliveryFeePerStore;
                    continue;
                }

                var storeDeliveryFee =
                    BaseDeliveryFee + (RatePerKm * (decimal)distanceKm.Value);

                totalDeliveryFee += Math.Round(storeDeliveryFee, 2);
            }

            if (totalDeliveryFee < 0)
            {
                return DefaultDeliveryFeePerStore;
            }

            return Math.Round(totalDeliveryFee, 2);
        }

        private async Task<double?> TryGetDrivingDistanceKmAsync(
            double storeLat,
            double storeLng,
            double customerLat,
            double customerLng)
        {
            try
            {
                var url =
                    $"https://router.project-osrm.org/route/v1/driving/{storeLng},{storeLat};{customerLng},{customerLat}?overview=false";

                using var response = await DistanceHttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();

                using var json = await JsonDocument.ParseAsync(stream);

                if (!json.RootElement.TryGetProperty("routes", out var routes))
                {
                    return null;
                }

                if (routes.GetArrayLength() == 0)
                {
                    return null;
                }

                var distanceMeters = routes[0].GetProperty("distance").GetDouble();

                return distanceMeters / 1000.0;
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadCartAsync(int customerId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Images)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.Store)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
            {
                CartItems = new List<CustomerCartItemViewModel>();
                TotalAmount = 0;
                EstimatedDeliveryFee = 0;
                GrandTotal = 0;
                HasActiveAddress = false;
                return;
            }

            CartItems = cart.CartItems
                .OrderByDescending(ci => ci.AddedAt)
                .Select(ci => new CustomerCartItemViewModel
                {
                    CartItemID = ci.CartItemID,
                    ProductID = ci.ProductID,
                    ProductName = ci.Product != null ? ci.Product.ProductName : "Unknown Product",
                    StoreName = ci.Product != null && ci.Product.Store != null
                        ? ci.Product.Store.StoreName
                        : "Unknown Store",
                    Quantity = ci.Quantity,
                    UnitPrice = ci.PriceAtAddTime,
                    TotalPrice = ci.PriceAtAddTime * ci.Quantity,
                    AvailableStock = ci.Product != null ? ci.Product.Quantity : 0,
                    ImageUrl = ci.Product != null
                        ? ci.Product.Images
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.DisplayOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault() ?? "/images/no-image.png"
                        : "/images/no-image.png"
                })
                .ToList();

            TotalAmount = CartItems.Sum(x => x.TotalPrice);

            var customer = await _context.Customers
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.CustomerID == customerId);

            var address = customer?.Addresses
                .FirstOrDefault(a => a.IsDefault && a.IsActive)
                ?? customer?.Addresses.FirstOrDefault(a => a.IsActive);

            HasActiveAddress = address != null;

            if (address != null)
            {
                EstimatedDeliveryFee = await CalculateDeliveryFeeAsync(
                    cart.CartItems.ToList(),
                    address,
                    TotalAmount);
            }
            else
            {
                EstimatedDeliveryFee = 0m;
            }

            GrandTotal = TotalAmount + EstimatedDeliveryFee;
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            return customer?.CustomerID;
        }
    }

    public class CustomerCartItemViewModel
    {
        public int CartItemID { get; set; }

        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public int AvailableStock { get; set; }

        public string ImageUrl { get; set; } = "/images/no-image.png";
    }
}