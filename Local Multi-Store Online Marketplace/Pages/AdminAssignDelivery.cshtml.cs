using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

        public List<OrderAssignViewModel> Orders { get; set; } = new();

        public List<DeliveryPersonAssignViewModel> DeliveryPeople { get; set; } = new();

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

            var assignment = new Multi_Store.Core.Entities.DeliveryAssignment
            {
                OrderID = orderId,
                DeliveryPersonID = deliveryPersonId,
                AssignedAt = DateTime.UtcNow,
                Status = "Assigned",
                DeliveryProofImageURL = null
            };

            _context.DeliveryAssignments.Add(assignment);

            // According to Delivery Assignment use case:
            // after assigning delivery staff, the order becomes Out for Delivery.
            order.Status = "Out for Delivery";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Delivery person assigned successfully to order {order.OrderNumber}. Order is now Out for Delivery.";

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
                .Select(o => new OrderAssignViewModel
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
                .Select(d => new DeliveryPersonAssignViewModel
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

    public class OrderAssignViewModel
    {
        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public int CustomerID { get; set; }
    }

    public class DeliveryPersonAssignViewModel
    {
        public int DeliveryPersonID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;
    }
}