using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

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

        public List<AdminAssignOrderViewModel> Orders { get; set; } = new();

        public List<AdminAssignDeliveryPersonViewModel> DeliveryPeople { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAssignAsync(int orderId, int deliveryPersonId)
        {
            if (orderId <= 0 || deliveryPersonId <= 0)
            {
                TempData["Error"] = "Please select a valid order and delivery person.";
                return RedirectToPage();
            }

            var order = await _context.Orders
                .Include(o => o.DeliveryAssignment)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToPage();
            }

            if (order.Status == "Delivered" || order.Status == "Cancelled")
            {
                TempData["Error"] = "Delivered or cancelled orders cannot be assigned.";
                return RedirectToPage();
            }

            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.DeliveryPersonID == deliveryPersonId &&
                    d.IsActive &&
                    d.Status == "Approved");

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Delivery person is not approved or active.";
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
                TempData["Error"] = "This order already has an active delivery assignment.";
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

            // Admin assigns only. Delivery starts later from Delivery Dashboard.
            order.Status = "Assigned";

            var notificationAlreadyExists = await _context.Notifications
                .AnyAsync(n =>
                    n.UserID == deliveryPerson.UserID &&
                    n.Type == "DeliveryAssignment" &&
                    n.ReferenceID == order.OrderID);

            if (!notificationAlreadyExists)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = deliveryPerson.UserID,
                    Title = "New Order Assigned",
                    Message = $"You have a new delivery order assigned: {order.OrderNumber}.",
                    Type = "DeliveryAssignment",
                    ReferenceID = order.OrderID,
                    IsRead = false,
                    SentAt = now,
                    SentVia = "System"
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Delivery person assigned successfully to order {order.OrderNumber}. Notification sent to {deliveryPerson.FullName}.";

            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            Orders = await _context.Orders
                .Include(o => o.Customer)
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
                .Select(o => new AdminAssignOrderViewModel
                {
                    OrderID = o.OrderID,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    CustomerID = o.CustomerID
                })
                .ToListAsync();

            DeliveryPeople = await _context.DeliveryPersons
                .Where(d =>
                    d.IsActive &&
                    d.Status == "Approved")
                .OrderBy(d => d.FullName)
                .Select(d => new AdminAssignDeliveryPersonViewModel
                {
                    DeliveryPersonID = d.DeliveryPersonID,
                    FullName = d.FullName,
                    PhoneNumber = d.PhoneNumber,
                    Area = d.Area,
                    VehicleType = d.VehicleType
                })
                .ToListAsync();
        }
    }

    public class AdminAssignOrderViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public int CustomerID { get; set; }
    }

    public class AdminAssignDeliveryPersonViewModel
    {
        public int DeliveryPersonID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;
    }
}