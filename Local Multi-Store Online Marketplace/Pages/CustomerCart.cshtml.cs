using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerCartModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CustomerCartModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<CustomerCartItemViewModel> CartItems { get; set; } = new();

        public decimal TotalAmount { get; set; }

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

            if (cartItem.Product.Quantity < quantity)
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
            var deliveryFee = 0m;
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

            _context.CartItems.RemoveRange(cart.CartItems);

            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Order placed successfully.";

            return RedirectToPage("/CustomerOrders");
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
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

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