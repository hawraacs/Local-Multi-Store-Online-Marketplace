using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;
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

        // Stateless helper with no dependencies of its own, so it's
        // instantiated directly here rather than requiring a DI
        // registration change in Program.cs.
        private readonly AreaProximityService _areaProximityService =
            new AreaProximityService();

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
        .Include(o => o.Address)
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
            // DELIVERY REGION VALIDATION
            // ==========================================
            if (order.Address == null ||
                string.IsNullOrWhiteSpace(order.Address.Area))
            {
                TempData["Error"] =
                    "Order delivery region is missing.";

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
                    .Include(o => o.OrderItems)
                    .Include(o => o.Address)
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

                        // Reuses the User.FullName property already
                        // loaded via the existing Customer -> User
                        // Include above. No new query needed.
                        CustomerName =
                            string.IsNullOrWhiteSpace(
                                order.Customer.User.FullName)
                                ? "N/A"
                                : order.Customer.User.FullName,

                        // ==================================
                        // PRODUCTS FOR THIS ORDER
                        //
                        // Same mapping already used by
                        // CustomerOrders.cshtml.cs (OrderItems ->
                        // ProductName / Quantity), reused here so the
                        // admin can see what's in the order.
                        // ==================================
                        Products =
                            order.OrderItems
                                .OrderBy(orderItem =>
                                    orderItem.OrderItemID)
                                .Select(orderItem =>
                                    new AdminAssignProductViewModel
                                    {
                                        ProductName =
                                            orderItem.ProductName,

                                        Quantity =
                                            orderItem.Quantity
                                    })
                                .ToList(),

                        // ==================================
                        // DELIVERY REGION FOR THIS ORDER
                        //
                        // Reuses the same Order.Address.Area field
                        // already used elsewhere (e.g. delivery order
                        // details) to represent the delivery region.
                        // ==================================
                        DeliveryRegion =
                            order.Address != null &&
                            !string.IsNullOrWhiteSpace(order.Address.Area)
                                ? order.Address.Area
                                : "N/A",

                        // ==================================
                        // DELIVERY LIST FOR THIS ORDER
                        //
                        // Ranked Same Area (3) / Nearby (2) / Far (1)
                        // via AreaProximityService, so the most
                        // suitable delivery person shows first.
                        //
                        // Also keeps the existing rule that prevents a
                        // customer from delivering their own order.
                        // ==================================
                        AvailableDeliveryPeople =
                            deliveryPeople
                                .Where(delivery =>
                                    !delivery.RequestedByUserID.HasValue ||
                                    delivery.RequestedByUserID.Value != order.Customer.UserID)
                                .Select(delivery => new
                                {
                                    delivery,
                                    priority = _areaProximityService.GetPriority(
                                        order.Address?.Area,
                                        delivery.Area)
                                })
                                .OrderByDescending(x => x.priority)
                                .ThenBy(x => x.delivery.FullName)
                                .Select(x =>
                                    new AdminAssignDeliveryPersonViewModel
                                    {
                                        DeliveryPersonID =
                                            x.delivery.DeliveryPersonID,

                                        RequestedByUserID =
                                            x.delivery.RequestedByUserID,

                                        FullName =
                                            x.delivery.FullName,

                                        PhoneNumber =
                                            x.delivery.PhoneNumber,

                                        Area =
                                            x.delivery.Area,

                                        VehicleType =
                                            x.delivery.VehicleType,

                                        AreaPriority =
                                            x.priority,

                                        AreaLabel =
                                            _areaProximityService.GetLabel(x.priority)
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

        public string CustomerName { get; set; }
            = string.Empty;

        public List<AdminAssignProductViewModel> Products { get; set; }
            = new();

        public string DeliveryRegion { get; set; }
            = string.Empty;

        public List<AdminAssignDeliveryPersonViewModel>
            AvailableDeliveryPeople
        { get; set; }
            = new();
    }

    // ==========================================
    // PRODUCT LINE VIEW MODEL
    //
    // Mirrors CustomerOrderProductViewModel in
    // CustomerOrders.cshtml.cs (ProductName + Quantity),
    // reused here for consistency.
    // ==========================================
    public class AdminAssignProductViewModel
    {
        public string ProductName { get; set; }
            = string.Empty;

        public int Quantity { get; set; }
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

        public int AreaPriority { get; set; }

        public string AreaLabel { get; set; }
            = string.Empty;
    }
}
