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

        public AdminAssignDeliveryModel(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // Every order has its own eligible delivery list.
        public List<AdminAssignOrderViewModel> Orders { get; set; }
            = new();

        // ==========================================
        // GET
        // ==========================================
        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        // ==========================================
        // ASSIGN DELIVERY PERSON TO ORDER
        // ==========================================
        public async Task<IActionResult> OnPostAssignAsync(
            int orderId,
            int deliveryPersonId)
        {
            if (orderId <= 0)
            {
                TempData["Error"] =
                    "Please select a valid order.";

                return RedirectToPage();
            }

            if (deliveryPersonId <= 0)
            {
                TempData["Error"] =
                    "Please select a valid delivery person.";

                return RedirectToPage();
            }

            var order =
                await _context.Orders
                    .Include(o => o.Customer)
                        .ThenInclude(c => c.User)
                    .Include(o => o.DeliveryAssignment)
                    .FirstOrDefaultAsync(o =>
                        o.OrderID == orderId);

            if (order == null)
            {
                TempData["Error"] =
                    "Order not found.";

                return RedirectToPage();
            }

            if (order.Customer == null)
            {
                TempData["Error"] =
                    "Customer linked to this order was not found.";

                return RedirectToPage();
            }

            if (order.Customer.User == null)
            {
                TempData["Error"] =
                    "Customer user account linked to this order was not found.";

                return RedirectToPage();
            }

            if (StatusEquals(
                    order.Status,
                    "Delivered") ||
                StatusEquals(
                    order.Status,
                    "Cancelled"))
            {
                TempData["Error"] =
                    "Delivered or cancelled orders cannot be assigned.";

                return RedirectToPage();
            }

            var deliveryPerson =
                await _context.DeliveryPersons
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

            // ==========================================
            // PREVENT SELF-DELIVERY
            //
            // Block only when RequestedByUserID exists
            // and belongs to the same customer.
            //
            // Old records with RequestedByUserID = NULL
            // remain available for assignment.
            // ==========================================
            if (deliveryPerson.RequestedByUserID.HasValue &&
                deliveryPerson.RequestedByUserID.Value ==
                order.Customer.UserID)
            {
                TempData["Error"] =
                    "This delivery person cannot be assigned " +
                    "to their own customer order.";

                return RedirectToPage();
            }

            // Check the navigation property first.
            if (order.DeliveryAssignment != null)
            {
                TempData["Error"] =
                    "This order already has a delivery assignment.";

                return RedirectToPage();
            }

            // Check the database table as additional protection.
            var activeAssignmentAlreadyExists =
                await _context.DeliveryAssignments
                    .AnyAsync(a =>
                        a.OrderID == orderId &&
                        a.Status != "Delivered" &&
                        a.Status != "Cancelled" &&
                        a.Status != "Failed");

            if (activeAssignmentAlreadyExists)
            {
                TempData["Error"] =
                    "This order already has an active " +
                    "delivery assignment.";

                return RedirectToPage();
            }

            var now = DateTime.UtcNow;

            var assignment = new DeliveryAssignment
            {
                OrderID = order.OrderID,

                DeliveryPersonID =
                    deliveryPerson.DeliveryPersonID,

                AssignedAt = now,
                PickupTime = null,
                DeliveryTime = null,
                Status = "Assigned",
                DeliveryProofImageURL = null
            };

            _context.DeliveryAssignments.Add(assignment);

            // Admin assigns the order only.
            // Delivery starts later from DeliveryDashboard.
            order.Status = "Assigned";

            // ==========================================
            // DELIVERY NOTIFICATION
            //
            // deliveryPerson.UserID belongs to the
            // generated Delivery login account.
            // ==========================================
            var notificationAlreadyExists =
                await _context.Notifications
                    .AnyAsync(n =>
                        n.UserID == deliveryPerson.UserID &&
                        n.Type == "DeliveryAssignment" &&
                        n.ReferenceID == order.OrderID);

            if (!notificationAlreadyExists)
            {
                var notification = new Notification
                {
                    UserID =
                        deliveryPerson.UserID,

                    Title =
                        "New Order Assigned",

                    Message =
                        $"You have a new delivery order " +
                        $"assigned: {order.OrderNumber}.",

                    Type =
                        "DeliveryAssignment",

                    ReferenceID =
                        order.OrderID,

                    IsRead =
                        false,

                    SentAt =
                        now,

                    SentVia =
                        "System"
                };

                _context.Notifications.Add(notification);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "The order could not be assigned. " +
                    "It may already have been assigned by another admin.";

                return RedirectToPage();
            }

            TempData["Success"] =
                $"Delivery person {deliveryPerson.FullName} " +
                $"was assigned successfully to order " +
                $"{order.OrderNumber}.";

            return RedirectToPage();
        }

        // ==========================================
        // LOAD ORDERS AND DELIVERY PEOPLE
        // ==========================================
        private async Task LoadDataAsync()
        {
            // Load every approved and active delivery person.
            // RequestedByUserID is NOT required here because
            // old records may still contain NULL.
            var deliveryPeople =
                await _context.DeliveryPersons
                    .AsNoTracking()
                    .Where(d =>
                        d.IsActive &&
                        d.Status == "Approved")
                    .OrderBy(d =>
                        d.FullName)
                    .ToListAsync();

            var orderEntities =
                await _context.Orders
                    .AsNoTracking()
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
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            Orders = orderEntities
                .Where(order =>
                    order.Customer != null &&
                    order.Customer.User != null)
                .Select(order =>
                    new AdminAssignOrderViewModel
                    {
                        OrderID =
                            order.OrderID,

                        OrderNumber =
                            order.OrderNumber,

                        OrderDate =
                            order.OrderDate,

                        Status =
                            order.Status,

                        TotalAmount =
                            order.TotalAmount,

                        CustomerID =
                            order.CustomerID,

                        CustomerUserID =
                            order.Customer.UserID,

                        // ==================================
                        // DELIVERY LIST FOR THIS ORDER
                        //
                        // NULL RequestedByUserID:
                        // show the delivery person.
                        //
                        // Different customer:
                        // show the delivery person.
                        //
                        // Same original customer:
                        // hide the delivery person.
                        // ==================================
                        AvailableDeliveryPeople =
                            deliveryPeople
                                .Where(delivery =>
                                    !delivery.RequestedByUserID.HasValue ||
                                    delivery.RequestedByUserID.Value !=
                                    order.Customer.UserID)
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

        // ==========================================
        // STATUS HELPER
        // ==========================================
        private static bool StatusEquals(
            string? currentStatus,
            string expectedStatus)
        {
            return string.Equals(
                currentStatus?.Trim(),
                expectedStatus,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ==========================================
    // ORDER VIEW MODEL
    // ==========================================
    public class AdminAssignOrderViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; }
            = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; }
            = string.Empty;

        public decimal TotalAmount { get; set; }

        public int CustomerID { get; set; }

        public int CustomerUserID { get; set; }

        public List<AdminAssignDeliveryPersonViewModel>
            AvailableDeliveryPeople
        { get; set; }
            = new();
    }

    // ==========================================
    // DELIVERY PERSON VIEW MODEL
    // ==========================================
    public class AdminAssignDeliveryPersonViewModel
    {
        public int DeliveryPersonID { get; set; }

        public int? RequestedByUserID { get; set; }

        public string FullName { get; set; }
            = string.Empty;

        public string PhoneNumber { get; set; }
            = string.Empty;

        public string Area { get; set; }
            = string.Empty;

        public string VehicleType { get; set; }
            = string.Empty;
    }
}
