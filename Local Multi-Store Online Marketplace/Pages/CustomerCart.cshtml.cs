using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services;
using Local_Multi_Store_Online_Marketplace.Hubs;
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
        private readonly IHubContext<AppHub> _hub; // NEW — pushes new orders live to Order/Index

        private const decimal FreeDeliveryThreshold = 50m;
        private const decimal BaseDeliveryFee = 2.00m;
        private const decimal RatePerKm = 0.50m;
        private const decimal DefaultDeliveryFeePerStore = 3.00m;

        // A cart item's product is treated as "Best Selling" (for the
        // Instagram-style filter chip on the cart page) once it has sold
        // at least this many units across all orders, store-wide.
        // Tune this to match what "best selling" should mean for the
        // marketplace.
        private const int BestSellingSalesThreshold = 10;

        // A cart item is treated as "Almost Out of Stock" (for the
        // filter chip) once its remaining stock drops to or below this
        // number of units.
        private const int LowStockCartThreshold = 5;

        private static readonly HttpClient DistanceHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        public CustomerCartModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            SubscriptionService subscriptionService,
            IHubContext<AppHub> hub) // NEW
        {
            _context = context;
            _userManager = userManager;
            _subscriptionService = subscriptionService;
            _hub = hub; // NEW
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

        // Comma-separated CartItemIDs the customer checked on the
        // Shein-style cart UI (only these are ordered at checkout;
        // everything else stays in the cart). Flows through GET
        // redirects too, so it survives the "add address, come
        // back and finish checkout" round-trip.
        [BindProperty(SupportsGet = true)]
        public string? SelectedCartItemIds { get; set; }

        // NEW — JSON map of { "cartItemId": "note text" } for the
        // items being checked out. Carried the same way as
        // SelectedCartItemIds so it survives the address round-trip.
        // Notes themselves are NOT stored on CartItem/OrderItem — they
        // live in the separate CartItemNotes / OrderItemNotes tables.
        [BindProperty(SupportsGet = true)]
        public string? ItemNotesJson { get; set; }

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
                    PaymentMethod,
                    SelectedCartItemIds,
                    ItemNotesJson);
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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please login as a customer first."
                    })
                    { StatusCode = StatusCodes.Status401Unauthorized };
                }

                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (quantity <= 0)
            {
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Quantity must be greater than 0."
                    });
                }

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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Cart item not found."
                    });
                }

                TempData["Error"] =
                    "Cart item not found.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            if (cartItem.Product == null ||
                cartItem.Product.Quantity < quantity)
            {
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Not enough stock available."
                    });
                }

                TempData["Error"] =
                    "Not enough stock available.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            cartItem.Quantity = quantity;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                // Reuse the exact same summary calculation logic
                // used on page load so every derived total (items,
                // subtotal, discount, delivery fee, grand total)
                // stays perfectly in sync with the server.
                await LoadCartAsync(customerId.Value);

                return new JsonResult(new
                {
                    success = true,
                    itemsCount = CartItems.Sum(item => item.Quantity),
                    totalAmount = TotalAmount,
                    hasActiveAddress = HasActiveAddress,
                    estimatedDeliveryFee = EstimatedDeliveryFee,
                    freeDeliveryApplied = TotalAmount > FreeDeliveryThreshold,
                    discountAmount = DiscountAmount,
                    couponMessage = CouponMessage,
                    grandTotal = GrandTotal,
                    updatedItem = new
                    {
                        cartItemId = cartItem.CartItemID,
                        quantity = cartItem.Quantity,
                        lineTotal = cartItem.PriceAtAddTime * cartItem.Quantity
                    }
                });
            }

            TempData["Success"] =
                "Cart updated successfully.";

            return RedirectToPage(
                new { AppliedCouponCode });
        }

        // =====================================================
        // UPDATE NOTE — stored in the separate CartItemNotes
        // table, not on CartItem itself. No entity/migration
        // changes to CartItem or OrderItem.
        // =====================================================
        public async Task<IActionResult> OnPostUpdateNoteAsync(
            int cartItemId,
            string? note)
        {
            var customerId = await GetCurrentCustomerIdAsync();

            if (customerId == null)
            {
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please login as a customer first."
                    })
                    { StatusCode = StatusCodes.Status401Unauthorized };
                }

                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            // Ownership check — confirm this cart item really
            // belongs to the logged-in customer before writing a note.
            var ownsCartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .AnyAsync(ci =>
                    ci.CartItemID == cartItemId &&
                    ci.Cart.CustomerID == customerId.Value);

            if (!ownsCartItem)
            {
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Cart item not found."
                    });
                }

                TempData["Error"] =
                    "Cart item not found.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            const int maxNoteLength = 200;

            var trimmedNote =
                string.IsNullOrWhiteSpace(note)
                    ? null
                    : note.Trim();

            if (trimmedNote != null &&
                trimmedNote.Length > maxNoteLength)
            {
                trimmedNote = trimmedNote[..maxNoteLength];
            }

            var existingNote = await _context.CartItemNotes
                .FirstOrDefaultAsync(n =>
                    n.CartItemID == cartItemId);

            if (trimmedNote == null)
            {
                if (existingNote != null)
                {
                    _context.CartItemNotes.Remove(existingNote);
                }
            }
            else if (existingNote != null)
            {
                existingNote.Note = trimmedNote;
                existingNote.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.CartItemNotes.Add(new CartItemNote
                {
                    CartItemID = cartItemId,
                    Note = trimmedNote,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                return new JsonResult(new
                {
                    success = true,
                    cartItemId,
                    note = trimmedNote
                });
            }

            TempData["Success"] = "Note saved.";

            return RedirectToPage(
                new { AppliedCouponCode });
        }

        // =====================================================
        // AJAX REQUEST DETECTION
        // =====================================================
        private bool IsAjaxRequest()
        {
            return string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please login as a customer first."
                    })
                    { StatusCode = StatusCodes.Status401Unauthorized };
                }

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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Cart item not found."
                    });
                }

                TempData["Error"] =
                    "Cart item not found.";

                return RedirectToPage(
                    new { AppliedCouponCode });
            }

            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            _context.CartItems.Remove(cartItem);

            // NEW — clean up any note tied to this cart item so
            // CartItemNotes doesn't accumulate orphaned rows.
            var noteToRemove = await _context.CartItemNotes
                .FirstOrDefaultAsync(n => n.CartItemID == cartItemId);

            if (noteToRemove != null)
            {
                _context.CartItemNotes.Remove(noteToRemove);
            }

            await _context.SaveChangesAsync();

            if (IsAjaxRequest())
            {
                // Used by the "Remove Selected" bulk-trash action,
                // which fires one fetch per checked item and then
                // reloads the page once every request settles.
                return new JsonResult(new { success = true });
            }

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

            // NEW — clean up notes for every cleared cart item.
            var clearedCartItemIds = cart.CartItems
                .Select(ci => ci.CartItemID)
                .ToList();

            if (clearedCartItemIds.Any())
            {
                var notesToRemove = await _context.CartItemNotes
                    .Where(n => clearedCartItemIds.Contains(n.CartItemID))
                    .ToListAsync();

                _context.CartItemNotes.RemoveRange(notesToRemove);
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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please login as a customer first."
                    })
                    { StatusCode = StatusCodes.Status401Unauthorized };
                }

                TempData["Error"] =
                    "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please enter a coupon code."
                    });
                }

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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Your cart is empty."
                    });
                }

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
                if (IsAjaxRequest())
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                TempData["Error"] =
                    result.Message;

                return RedirectToPage();
            }

            var successMessage =
                $"Coupon {cleanCode} applied successfully. " +
                $"Discount: ${result.DiscountAmount:N2}.";

            if (IsAjaxRequest())
            {
                // Reuse the exact same summary calculation logic used
                // everywhere else (quantity update, page load) so every
                // derived total stays perfectly in sync with the server.
                // AppliedCouponCode is set here purely so LoadCartAsync
                // (called below, in-process) picks up the just-applied
                // code — the non-AJAX branch below is untouched and
                // still gets the code from the redirect's query string
                // exactly as before.
                AppliedCouponCode = cleanCode;

                await LoadCartAsync(customerId.Value);

                return new JsonResult(new
                {
                    success = true,
                    message = successMessage,
                    appliedCouponCode = cleanCode,
                    itemsCount = CartItems.Sum(item => item.Quantity),
                    totalAmount = TotalAmount,
                    hasActiveAddress = HasActiveAddress,
                    estimatedDeliveryFee = EstimatedDeliveryFee,
                    freeDeliveryApplied = TotalAmount > FreeDeliveryThreshold,
                    discountAmount = DiscountAmount,
                    couponMessage = CouponMessage,
                    grandTotal = GrandTotal
                });
            }

            TempData["Success"] =
                successMessage;

            return RedirectToPage(
                new
                {
                    AppliedCouponCode = cleanCode
                });
        }

        // =====================================================
        // REMOVE COUPON
        // =====================================================
        public async Task<IActionResult> OnPostRemoveCoupon()
        {
            if (IsAjaxRequest())
            {
                var customerId = await GetCurrentCustomerIdAsync();

                if (customerId == null)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Please login as a customer first."
                    })
                    { StatusCode = StatusCodes.Status401Unauthorized };
                }

                // AppliedCouponCode defaults to null on this fresh
                // model instance (no query string on an AJAX POST),
                // so LoadCartAsync naturally recalculates with no
                // coupon applied — identical end state to what the
                // existing non-AJAX redirect below already produces.
                await LoadCartAsync(customerId.Value);

                return new JsonResult(new
                {
                    success = true,
                    message = "Coupon removed.",
                    appliedCouponCode = (string?)null,
                    itemsCount = CartItems.Sum(item => item.Quantity),
                    totalAmount = TotalAmount,
                    hasActiveAddress = HasActiveAddress,
                    estimatedDeliveryFee = EstimatedDeliveryFee,
                    freeDeliveryApplied = TotalAmount > FreeDeliveryThreshold,
                    discountAmount = DiscountAmount,
                    couponMessage = CouponMessage,
                    grandTotal = GrandTotal
                });
            }

            TempData["Success"] =
                "Coupon removed.";

            return RedirectToPage();
        }

        // =====================================================
        // CHECKOUT
        // =====================================================
        public async Task<IActionResult> OnPostCheckoutAsync(
            string? appliedCouponCode,
            string? paymentMethod,
            string? selectedCartItemIds,
            string? itemNotesJson)
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
                paymentMethod,
                selectedCartItemIds,
                itemNotesJson);
        }

        // =====================================================
        // PARSE SELECTED CART ITEM IDS
        // (comma-separated string coming from the Shein-style
        // checkbox selection on the cart page)
        // =====================================================
        private static List<int> ParseSelectedCartItemIds(
            string? rawSelectedIds)
        {
            if (string.IsNullOrWhiteSpace(rawSelectedIds))
            {
                return new List<int>();
            }

            return rawSelectedIds
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(part =>
                    int.TryParse(part, out var value)
                        ? value
                        : (int?)null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToList();
        }

        // =====================================================
        // PLACE ORDER
        // Only the CartItems whose IDs appear in
        // selectedCartItemIds are turned into an order; every
        // other cart item is left untouched in the customer's
        // cart. If no selection is provided (e.g. an older client
        // or a direct call), the whole cart is checked out, to
        // stay backward compatible.
        // =====================================================
        private async Task<IActionResult> PlaceOrderFromCartAsync(
            int customerId,
            string? appliedCouponCode,
            string? paymentMethod,
            string? selectedCartItemIds,
            string? itemNotesJson)
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

            // CHANGED — added .Include(c => c.User) so the customer's
            // display name is available for the live-order broadcast
            // below, without an extra round trip.
            var customer = await _context.Customers
                .Include(c => c.Addresses)
                .Include(c => c.User)
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

            // =================================================
            // RESOLVE WHICH CART ITEMS ARE ACTUALLY BEING
            // CHECKED OUT (the Shein-style "selected only" set)
            // =================================================
            var selectedIds =
                ParseSelectedCartItemIds(selectedCartItemIds);

            var itemsToCheckout = selectedIds.Any()
                ? cart.CartItems
                    .Where(ci => selectedIds.Contains(ci.CartItemID))
                    .ToList()
                : cart.CartItems.ToList();

            if (!itemsToCheckout.Any())
            {
                TempData["Error"] =
                    "Please select at least one item to checkout.";

                return RedirectToPage(
                    new
                    {
                        AppliedCouponCode =
                            appliedCouponCode
                    });
            }

            // NEW — load any per-item notes for the items being
            // checked out, from the separate CartItemNotes table.
            var checkoutCartItemIds = itemsToCheckout
                .Select(item => item.CartItemID)
                .ToList();

            var checkoutNotesByCartItemId = checkoutCartItemIds.Any()
                ? await _context.CartItemNotes
                    .Where(n => checkoutCartItemIds.Contains(n.CartItemID))
                    .ToDictionaryAsync(n => n.CartItemID, n => n.Note)
                : new Dictionary<int, string>();

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

                var encodedSelectedIds =
                    Uri.EscapeDataString(
                        string.Join(
                            ",",
                            itemsToCheckout.Select(
                                item => item.CartItemID)));

                var encodedItemNotesJson =
                    Uri.EscapeDataString(
                        itemNotesJson ?? string.Empty);

                return RedirectToPage(
                    "/CustomerAddresses",
                    new
                    {
                        returnUrl =
                            $"/CustomerCart" +
                            $"?CheckoutAfterAddress=true" +
                            $"&AppliedCouponCode={encodedCoupon}" +
                            $"&PaymentMethod={encodedPayment}" +
                            $"&SelectedCartItemIds={encodedSelectedIds}" +
                            $"&ItemNotesJson={encodedItemNotesJson}"
                    });
            }

            // =================================================
            // VALIDATE PRODUCTS, SUBSCRIPTIONS, AND STOCK
            // (only for the items actually being checked out)
            // =================================================
            foreach (var item in itemsToCheckout)
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

            var subtotal = itemsToCheckout.Sum(
                item =>
                    item.PriceAtAddTime *
                    item.Quantity);

            var deliveryFee =
                await CalculateDeliveryFeeAsync(
                    itemsToCheckout,
                    address,
                    subtotal);

            var couponResult =
                await CalculateCouponDiscountAsync(
                    appliedCouponCode,
                    itemsToCheckout,
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
            // (only for the checked-out items)
            // =================================================
            foreach (var item in itemsToCheckout.ToList())
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

                // NEW — carry the cart-item note over to the
                // separate OrderItemNotes table, keyed by
                // OrderID + ProductID.
                if (checkoutNotesByCartItemId.TryGetValue(
                        item.CartItemID,
                        out var itemNote) &&
                    !string.IsNullOrWhiteSpace(itemNote))
                {
                    _context.OrderItemNotes.Add(new OrderItemNote
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Note = itemNote,
                        CreatedAt = DateTime.UtcNow
                    });
                }

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
            // NEW ORDER NOTIFICATION — one per store involved.
            // A cart can span multiple stores, so every store whose
            // products were just purchased gets its own notification
            // (this was the missing piece — everything else in this
            // method already notified admins/delivery/low-stock, but
            // nothing ever told the store owner "you got an order").
            // Only stores present in the CHECKED-OUT items get
            // notified — stores whose items were left in the cart
            // are not.
            //
            // NEW — each involved store also gets a live "NewOrderPlaced"
            // broadcast via the shared AppHub, so the store owner's
            // Order/Index page can show the new order instantly without
            // a refresh, the same way notifications already do — just
            // without needing to visit ReportUpdates first.
            // =================================================
            var involvedStoreIds = itemsToCheckout
                .Where(item => item.Product != null)
                .Select(item => item.Product!.StoreID)
                .Distinct()
                .ToList();

            var customerDisplayName =
                customer.User?.FullName ??
                customer.User?.UserName ??
                "Customer";

            foreach (var storeId in involvedStoreIds)
            {
                var involvedStore = await _context.Stores
                    .FirstOrDefaultAsync(storeItem =>
                        storeItem.StoreID == storeId);

                if (involvedStore != null)
                {
                    var itemsForStore = itemsToCheckout
                        .Where(item =>
                            item.Product != null &&
                            item.Product.StoreID == storeId)
                        .ToList();

                    var itemCountForStore =
                        itemsForStore.Sum(item => item.Quantity);

                    var totalAmountForStore =
                        itemsForStore.Sum(item =>
                            item.PriceAtAddTime * item.Quantity);

                    _context.Notifications.Add(new Notification
                    {
                        UserID = involvedStore.OwnerUserID,

                        Title = "New Order Received",

                        Message =
                            $"You have a new order ({order.OrderNumber}) " +
                            $"with {itemCountForStore} item(s) waiting to be processed.",

                        Type = "NewOrder",

                        ReferenceID = order.OrderID,

                        IsRead = false,

                        SentAt = DateTime.UtcNow,

                        SentVia = "System"
                    });

                    // NEW — live push to Order/Index for this store owner.
                    try
                    {
                        await _hub.Clients.All.SendAsync("NewOrderPlaced", new
                        {
                            orderId = order.OrderID,
                            orderNumber = order.OrderNumber,
                            storeId = involvedStore.StoreID,
                            customerName = customerDisplayName,
                            orderDate = order.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                            itemCount = itemCountForStore,

                            // Store-scoped total (just this store's items
                            // within the order), matching what Order/Index
                            // already shows per row — not the full
                            // multi-vendor order.TotalAmount.
                            totalAmount = totalAmountForStore
                        });
                    }
                    catch
                    {
                        // Never let a broadcast failure break checkout —
                        // the order is already safely saved at this point.
                    }
                }
            }

            // =================================================
            // CLEAR ONLY THE CHECKED-OUT ITEMS FROM THE CART
            // Anything the customer left unchecked stays in the
            // cart for a later checkout.
            // =================================================
            _context.CartItems.RemoveRange(
                itemsToCheckout);

            // NEW — clean up notes for the checked-out cart items
            // now that they no longer exist as cart items (the note
            // has already been copied into OrderItemNotes above).
            if (checkoutNotesByCartItemId.Any())
            {
                var noteRowsToRemove = await _context.CartItemNotes
                    .Where(n =>
                        checkoutCartItemIds.Contains(n.CartItemID))
                    .ToListAsync();

                _context.CartItemNotes.RemoveRange(noteRowsToRemove);
            }

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
        private Task<decimal>
            CalculateDeliveryFeeAsync(
                List<CartItem> cartItems,
                CustomerAddress customerAddress,
                decimal subtotal)
        {
            if (subtotal >
                FreeDeliveryThreshold)
            {
                return Task.FromResult(0m);
            }

            // Fix: delivery fee is a single flat fee for the WHOLE order,
            // not per store. Previously this looped over every distinct
            // store in the cart and added a fee (fixed, default, or
            // distance-based) for EACH one, so a 2-store order became
            // $6, a 3-store order $9, etc. Business rule: 1 store, 2
            // stores, 10 stores — always exactly DefaultDeliveryFeePerStore
            // ($3) total for the order. cartItems/customerAddress are no
            // longer used here, but the parameters are left unchanged so
            // no caller needs to be touched. async/await removed since
            // this no longer awaits anything — Task.FromResult keeps the
            // exact same Task<decimal> signature every existing
            // "await CalculateDeliveryFeeAsync(...)" call site expects.
            return Task.FromResult(DefaultDeliveryFeePerStore);
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

                        // NEW — needed so the cart's "View" link can
                        // jump straight to this product inside the
                        // StoreCustomerProfile page.
                        StoreID =
                            item.Product != null
                                ? item.Product.StoreID
                                : 0,

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
                                : "/images/no-image.png",

                        IsAlmostOutOfStock =
                            item.Product != null &&
                            item.Product.Quantity <=
                                LowStockCartThreshold
                    })
                .ToList();

            // NEW — load notes for these cart items from the
            // separate CartItemNotes table (Note does NOT live
            // on CartItem itself).
            var cartItemIdsForNotes = CartItems
                .Select(item => item.CartItemID)
                .ToList();

            if (cartItemIdsForNotes.Any())
            {
                var notesByCartItemId = await _context.CartItemNotes
                    .Where(n => cartItemIdsForNotes.Contains(n.CartItemID))
                    .ToDictionaryAsync(n => n.CartItemID, n => n.Note);

                foreach (var cartItem in CartItems)
                {
                    if (notesByCartItemId.TryGetValue(
                            cartItem.CartItemID,
                            out var note))
                    {
                        cartItem.Note = note;
                    }
                }
            }

            TotalAmount =
                CartItems.Sum(item =>
                    item.TotalPrice);

            // =================================================
            // BEST SELLING FLAG (Instagram-style filter chip)
            // Computed from real sales history: how many units of
            // each product in the cart have ever been ordered,
            // across all customers.
            // =================================================
            var cartProductIds = CartItems
                .Select(item => item.ProductID)
                .Distinct()
                .ToList();

            if (cartProductIds.Any())
            {
                var totalSoldByProduct =
                    await _context.OrderItems
                        .Where(orderItem =>
                            cartProductIds.Contains(
                                orderItem.ProductID))
                        .GroupBy(orderItem =>
                            orderItem.ProductID)
                        .Select(group =>
                            new
                            {
                                ProductID = group.Key,
                                TotalSold =
                                    group.Sum(orderItem =>
                                        orderItem.Quantity)
                            })
                        .ToDictionaryAsync(
                            entry => entry.ProductID,
                            entry => entry.TotalSold);

                foreach (var cartItem in CartItems)
                {
                    cartItem.IsBestSeller =
                        totalSoldByProduct.TryGetValue(
                            cartItem.ProductID,
                            out var totalSold) &&
                        totalSold >= BestSellingSalesThreshold;
                }
            }

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

        // NEW — needed so the cart's "View" link can jump straight
        // to this product inside the StoreCustomerProfile page.
        public int StoreID { get; set; }

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

        // Drives the "Best Selling" filter chip on the cart page.
        // Computed from real order history — see LoadCartAsync.
        public bool IsBestSeller { get; set; }

        // Drives the "Almost Out of Stock" filter chip on the cart page.
        public bool IsAlmostOutOfStock { get; set; }

        // NEW — loaded from the separate CartItemNotes table.
        // Not a column on CartItem itself.
        public string? Note { get; set; }
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