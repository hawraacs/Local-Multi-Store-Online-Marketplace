using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAssignDeliveryModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdminAssignDeliveryModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Every order contains its own eligible delivery people.
        public List<AdminAssignOrderViewModel> Orders { get; set; } = new();

        // =========================
        // GET
        // =========================
        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        // =========================
        // ASSIGN DELIVERY
        // =========================
        public async Task<IActionResult> OnPostAssignAsync(
            int orderId,
            int deliveryPersonId)
        {
            if (orderId <= 0 || deliveryPersonId <= 0)
            {
                TempData["Error"] =
                    "Please select a valid order and delivery person.";

                return RedirectToPage();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.DeliveryAssignment)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToPage();
            }

            if (order.Customer == null || order.Customer.User == null)
            {
                TempData["Error"] =
                    "The customer account linked to this order was not found.";

                return RedirectToPage();
            }

            if (string.Equals(
                    order.Status,
                    "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    order.Status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] =
                    "Delivered or cancelled orders cannot be assigned.";

                return RedirectToPage();
            }

            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.DeliveryPersonID == deliveryPersonId &&
                    d.IsActive &&
                    d.Status == "Approved");

            if (deliveryPerson == null)
            {
                TempData["Error"] =
                    "Delivery person is not approved or active.";

                return RedirectToPage();
            }

            // =========================================
            // PREVENT SELF-DELIVERY
            // =========================================
            var sameOriginalCustomer =
                deliveryPerson.RequestedByUserID.HasValue &&
                deliveryPerson.RequestedByUserID.Value ==
                order.Customer.UserID;

            // Legacy records created before RequestedByUserID
            // are compared safely using the phone number.
            var sameLegacyCustomerByPhone =
                !deliveryPerson.RequestedByUserID.HasValue &&
                SamePhoneNumber(
                    deliveryPerson.PhoneNumber,
                    order.Customer.User.PhoneNumber);

            if (sameOriginalCustomer || sameLegacyCustomerByPhone)
            {
                TempData["Error"] =
                    "This delivery person cannot be assigned to their own customer order.";

                return RedirectToPage();
            }

            var alreadyAssigned = await _context.DeliveryAssignments
                .AnyAsync(a =>
                    a.OrderID == orderId &&
                    a.Status != "Delivered" &&
                    a.Status != "Cancelled" &&
                    a.Status != "Failed");

            if (alreadyAssigned)
            {
                TempData["Error"] =
                    "This order already has an active delivery assignment.";

                return RedirectToPage();
            }

            var now = DateTime.UtcNow;

            var assignment = new DeliveryAssignment
            {
                OrderID = orderId,
                DeliveryPersonID = deliveryPersonId,
                AssignedAt = now,
                PickupTime = null,
                DeliveryTime = null,
                Status = "Assigned",
                DeliveryProofImageURL = null
            };

            _context.DeliveryAssignments.Add(assignment);

            // The admin only assigns the order.
            // Delivery starts later from Delivery Dashboard.
            order.Status = "Assigned";

            var notificationAlreadyExists =
                await _context.Notifications.AnyAsync(n =>
                    n.UserID == deliveryPerson.UserID &&
                    n.Type == "DeliveryAssignment" &&
                    n.ReferenceID == order.OrderID);

            if (!notificationAlreadyExists)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = deliveryPerson.UserID,
                    Title = "New Order Assigned",
                    Message =
                        $"You have a new delivery order assigned: {order.OrderNumber}.",
                    Type = "DeliveryAssignment",
                    ReferenceID = order.OrderID,
                    IsRead = false,
                    SentAt = now,
                    SentVia = "System"
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Delivery person {deliveryPerson.FullName} was assigned successfully to order {order.OrderNumber}.";

            return RedirectToPage();
        }

        // =========================
        // LOAD DATA
        // =========================
        private async Task LoadDataAsync()
        {
            var deliveryPeople = await _context.DeliveryPersons
                .Where(d =>
                    d.IsActive &&
                    d.Status == "Approved")
                .OrderBy(d => d.FullName)
                .ToListAsync();

            var orderEntities = await _context.Orders
                .Include(o => o.Customer)
                    .ThenInclude(c => c.User)
                .Include(o => o.DeliveryAssignment)
                .Where(o =>
                    o.DeliveryAssignment == null &&
                    (
                        o.Status == "Pending" ||
                        o.Status == "Pending Confirmation" ||
                        o.Status == "Confirmed" ||
                        o.Status == "Preparing" ||
                        o.Status == "Ready for Pickup"
                    ))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            Orders = orderEntities
                .Where(order =>
                    order.Customer != null &&
                    order.Customer.User != null)
                .Select(order => new AdminAssignOrderViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    TotalAmount = order.TotalAmount,
                    CustomerID = order.CustomerID,
                    CustomerUserID = order.Customer.UserID,

                    // Create a separate delivery list for every order.
                    // The delivery person linked to this customer is removed.
                    AvailableDeliveryPeople = deliveryPeople
                        .Where(delivery =>
                        {
                            var sameOriginalCustomer =
                                delivery.RequestedByUserID.HasValue &&
                                delivery.RequestedByUserID.Value ==
                                order.Customer.UserID;

                            var sameLegacyCustomerByPhone =
                                !delivery.RequestedByUserID.HasValue &&
                                SamePhoneNumber(
                                    delivery.PhoneNumber,
                                    order.Customer.User.PhoneNumber);

                            return !sameOriginalCustomer &&
                                   !sameLegacyCustomerByPhone;
                        })
                        .Select(delivery =>
                            new AdminAssignDeliveryPersonViewModel
                            {
                                DeliveryPersonID =
                                    delivery.DeliveryPersonID,

                                RequestedByUserID =
                                    delivery.RequestedByUserID,

                                FullName =
                                    delivery.FullName,

                                PhoneNumber =
                                    delivery.PhoneNumber,

                                Area =
                                    delivery.Area,

                                VehicleType =
                                    delivery.VehicleType
                            })
                        .ToList()
                })
                .ToList();
        }

        // =========================
        // LEGACY PHONE COMPARISON
        // =========================
        private static bool SamePhoneNumber(
            string? firstPhone,
            string? secondPhone)
        {
            var first = NormalizePhone(firstPhone);
            var second = NormalizePhone(secondPhone);

            return !string.IsNullOrWhiteSpace(first) &&
                   !string.IsNullOrWhiteSpace(second) &&
                   first == second;
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return string.Empty;
            }

            var digits = new string(
                phone.Where(char.IsDigit).ToArray());

            if (string.IsNullOrWhiteSpace(digits))
            {
                return string.Empty;
            }

            if (digits.StartsWith("00961"))
            {
                digits = digits.Substring(5);
            }
            else if (digits.StartsWith("961"))
            {
                digits = digits.Substring(3);
            }

            if (!digits.StartsWith("0"))
            {
                digits = "0" + digits;
            }

            return digits;
        }
    }

    // =========================
    // ORDER VIEW MODEL
    // =========================
    public class AdminAssignOrderViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public int CustomerID { get; set; }

        public int CustomerUserID { get; set; }

        public List<AdminAssignDeliveryPersonViewModel>
            AvailableDeliveryPeople
        { get; set; } = new();
    }

    // =========================
    // DELIVERY VIEW MODEL
    // =========================
    public class AdminAssignDeliveryPersonViewModel
    {
        public int DeliveryPersonID { get; set; }

        public int? RequestedByUserID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;
    }
}