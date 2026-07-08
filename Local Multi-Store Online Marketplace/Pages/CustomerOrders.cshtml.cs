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

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer")]
    public class CustomerOrdersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CustomerOrdersModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Page();
        }

        public class CustomerOrderViewModel
        {
            public int OrderID { get; set; }

            public string OrderNumber { get; set; } = string.Empty;

            public List<CustomerOrderProductViewModel> Products { get; set; }
                = new();

            public DateTime OrderDate { get; set; }

            public string Status { get; set; } = string.Empty;

            public string PaymentMethod { get; set; } = string.Empty;

            public string PaymentStatus { get; set; } = string.Empty;

            public decimal TotalAmount { get; set; }

            public string? AssignmentStatus { get; set; }

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