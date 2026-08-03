using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly DeliveryReviewManager _deliveryReviewManager;

        public CustomerOrdersModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            DeliveryReviewManager deliveryReviewManager)
        {
            _context = context;
            _userManager = userManager;
            _deliveryReviewManager = deliveryReviewManager;
        }

        public List<CustomerOrderViewModel> Orders { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["Error"] = "Please login as a customer first.";

                return RedirectToPage(
                    "/Account/Login",
                    new { area = "Identity" }
                );
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile was not found.";
                Orders = new List<CustomerOrderViewModel>();

                return Page();
            }

            Orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerID == customer.CustomerID)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new CustomerOrderViewModel
                {
                    OrderID = o.OrderID,
                    OrderNumber = o.OrderNumber,

                    // Order has no direct Store navigation — StoreID
                    // lives on each OrderItem instead. Most orders will
                    // only have one distinct store, but we join all
                    // distinct store names in case an order ever spans
                    // items from more than one store.
                    StoreName = string.Join(
                        ", ",
                        o.OrderItems
                            .Select(orderItem => orderItem.Store.StoreName)
                            .Distinct()),

                    Products = o.OrderItems
                        .OrderBy(orderItem => orderItem.OrderItemID)
                        .Select(orderItem => new CustomerOrderProductViewModel
                        {
                            ProductName = orderItem.ProductName,
                            Quantity = orderItem.Quantity
                        })
                        .ToList(),

                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    TotalAmount = o.TotalAmount,

                    AssignmentStatus = _context.DeliveryAssignments
                        .Where(a =>
                            a.OrderID == o.OrderID &&
                            a.Status != "Cancelled" &&
                            a.Status != "Failed")
                        .OrderByDescending(a => a.AssignedAt)
                        .Select(a => a.Status)
                        .FirstOrDefault(),

                    // NEW — who delivered this order, and whether the
                    // customer has already reviewed that delivery.
                    DeliveryPersonID = _context.DeliveryAssignments
                        .Where(a =>
                            a.OrderID == o.OrderID &&
                            a.Status != "Cancelled" &&
                            a.Status != "Failed")
                        .OrderByDescending(a => a.AssignedAt)
                        .Select(a => (int?)a.DeliveryPersonID)
                        .FirstOrDefault(),

                    DeliveryPersonName = _context.DeliveryAssignments
                        .Where(a =>
                            a.OrderID == o.OrderID &&
                            a.Status != "Cancelled" &&
                            a.Status != "Failed")
                        .OrderByDescending(a => a.AssignedAt)
                        .Select(a => a.DeliveryPerson.FullName)
                        .FirstOrDefault(),

                    IsDeliveryReviewed = _context.DeliveryReviews
                        .Any(r => r.OrderID == o.OrderID)
                })
                .ToListAsync();

            // ================= DELIVERY CARD ENRICHMENT (NEW) =================
            // Kept as a separate post-processing step, on purpose: it never
            // touches the query above, so there is zero risk to the existing
            // tracking/invoice/order fields. Two small bulk lookups instead
            // of per-row subqueries.
            var deliveryPersonIds = Orders
                .Where(o => o.DeliveryPersonID.HasValue)
                .Select(o => o.DeliveryPersonID!.Value)
                .Distinct()
                .ToList();

            if (deliveryPersonIds.Count > 0)
            {
                var deliveryPersonInfo = await _context.DeliveryPersons
                    .Where(d => deliveryPersonIds.Contains(d.DeliveryPersonID))
                    .Select(d => new
                    {
                        d.DeliveryPersonID,
                        d.Area,
                        d.VehicleType,
                        d.Rating
                    })
                    .ToDictionaryAsync(d => d.DeliveryPersonID);

                var reviewCounts = await _context.DeliveryReviews
                    .Where(r => deliveryPersonIds.Contains(r.DeliveryPersonID))
                    .GroupBy(r => r.DeliveryPersonID)
                    .Select(g => new { DeliveryPersonID = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.DeliveryPersonID, g => g.Count);

                foreach (var order in Orders)
                {
                    if (!order.DeliveryPersonID.HasValue)
                    {
                        continue;
                    }

                    if (deliveryPersonInfo.TryGetValue(order.DeliveryPersonID.Value, out var info))
                    {
                        order.DeliveryPersonArea = info.Area;
                        order.DeliveryPersonVehicleType = info.VehicleType;
                        order.DeliveryPersonAverageRating = info.Rating;
                    }

                    if (reviewCounts.TryGetValue(order.DeliveryPersonID.Value, out var count))
                    {
                        order.DeliveryPersonReviewCount = count;
                    }
                }
            }

            var orderIds = Orders.Select(o => o.OrderID).ToList();

            var myRatings = await _context.DeliveryReviews
                .Where(r => orderIds.Contains(r.OrderID))
                .ToDictionaryAsync(r => r.OrderID, r => r.Rating);

            foreach (var order in Orders)
            {
                if (myRatings.TryGetValue(order.OrderID, out var myRating))
                {
                    order.MyDeliveryReviewRating = myRating;
                }
            }

            return Page();
        }

        // ================= SUBMIT DELIVERY REVIEW =================
        // Returns JSON (not RedirectToPage) so the modal can close and
        // the button can flip to "Reviewed" without a full page reload.
        //
        // NOTE: since this is called via fetch() rather than a plain
        // <form method="post"> with the auto-included antiforgery hidden
        // field, the calling JS must send the token itself — e.g. read
        // it from a hidden @Html.AntiForgeryToken() field and send it as
        // the "RequestVerificationToken" header on the fetch call.
        public async Task<IActionResult> OnPostSubmitDeliveryReviewAsync(
            int orderId,
            int rating,
            string? comment)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Please login again."
                })
                { StatusCode = 401 };
            }

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Customer profile was not found."
                })
                { StatusCode = 400 };
            }

            try
            {
                var result = await _deliveryReviewManager.AddReviewAsync(
                    orderId,
                    customer.CustomerID,
                    rating,
                    comment,
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    Request.Headers["User-Agent"].ToString());

                return new JsonResult(new
                {
                    success = true,
                    message = "Thanks for reviewing your delivery!",
                    deliveryPersonName = result.DeliveryPersonName,
                    rating = result.Rating
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = ex.Message
                })
                { StatusCode = 400 };
            }
        }

        public class CustomerOrderViewModel
        {
            public int OrderID { get; set; }

            public string OrderNumber { get; set; } = string.Empty;

            public string StoreName { get; set; } = string.Empty;

            public List<CustomerOrderProductViewModel> Products { get; set; }
                = new();

            public DateTime OrderDate { get; set; }

            public string Status { get; set; } = string.Empty;

            public string PaymentMethod { get; set; } = string.Empty;

            public string PaymentStatus { get; set; } = string.Empty;

            public decimal TotalAmount { get; set; }

            public string? AssignmentStatus { get; set; }

            // NEW — delivery-review support.
            public int? DeliveryPersonID { get; set; }

            public string? DeliveryPersonName { get; set; }

            public string? DeliveryPersonArea { get; set; }

            public string? DeliveryPersonVehicleType { get; set; }

            public decimal? DeliveryPersonAverageRating { get; set; }

            public int DeliveryPersonReviewCount { get; set; }

            public bool IsDeliveryReviewed { get; set; }

            // This customer's own submitted rating for this order, once
            // reviewed — lets the page render "Delivery Rated ✓ ★★★★★"
            // immediately on load, not just a boolean.
            public int? MyDeliveryReviewRating { get; set; }

            public string DeliveryPersonInitial =>
                string.IsNullOrWhiteSpace(DeliveryPersonName)
                    ? "D"
                    : DeliveryPersonName.Trim().Substring(0, 1).ToUpper();

            public bool CanReviewDelivery =>
                string.Equals(
                    Status?.Trim(),
                    "Delivered",
                    StringComparison.OrdinalIgnoreCase) &&
                DeliveryPersonID.HasValue &&
                !IsDeliveryReviewed;

            public bool HasDeliveryAssignment =>
                !string.IsNullOrWhiteSpace(AssignmentStatus);

            public bool CanTrack
            {
                get
                {
                    var cleanOrderStatus = Status?.Trim();
                    var cleanAssignmentStatus = AssignmentStatus?.Trim();

                    if (!HasDeliveryAssignment)
                    {
                        return false;
                    }

                    // Delivery is currently running.
                    if (string.Equals(
                            cleanOrderStatus,
                            "Out for Delivery",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            cleanAssignmentStatus,
                            "OutForDelivery",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Delivery finished, but the customer can still open tracking.
                    if (string.Equals(
                            cleanOrderStatus,
                            "Delivered",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            cleanAssignmentStatus,
                            "Delivered",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return false;
                }
            }

            public string TrackingMessage
            {
                get
                {
                    var cleanOrderStatus = Status?.Trim();
                    var cleanAssignmentStatus = AssignmentStatus?.Trim();

                    if (!HasDeliveryAssignment)
                    {
                        return "Available when out for delivery";
                    }

                    if (string.Equals(
                            cleanOrderStatus,
                            "Assigned",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            cleanAssignmentStatus,
                            "Assigned",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Available when delivery starts";
                    }

                    return "Available when out for delivery";
                }
            }

            public bool CanViewInvoice
            {
                get
                {
                    var cleanPaymentStatus = PaymentStatus?.Trim();
                    var cleanOrderStatus = Status?.Trim();

                    return string.Equals(
                               cleanPaymentStatus,
                               "Paid",
                               StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(
                               cleanOrderStatus,
                               "Delivered",
                               StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public class CustomerOrderProductViewModel
        {
            public string ProductName { get; set; } = string.Empty;

            public int Quantity { get; set; }
        }
    }
}
