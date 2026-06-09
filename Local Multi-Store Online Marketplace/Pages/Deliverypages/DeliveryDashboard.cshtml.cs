using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public DeliveryDashboardModel(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<DeliveryAssignmentViewModel> Assignments { get; set; } = new();

        public List<DeliveryNotificationViewModel> Notifications { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage = "Approved delivery profile was not found.";
                Assignments = new List<DeliveryAssignmentViewModel>();
                Notifications = new List<DeliveryNotificationViewModel>();
                return Page();
            }

            Assignments = await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o.Address)
                .Where(a =>
                    a.DeliveryPersonID == deliveryPerson.DeliveryPersonID &&
                    a.Status != "Delivered" &&
                    a.Status != "Cancelled" &&
                    a.Status != "Failed")
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => new DeliveryAssignmentViewModel
                {
                    AssignmentID = a.AssignmentID,
                    OrderID = a.OrderID,
                    OrderNumber = a.Order != null ? a.Order.OrderNumber : string.Empty,
                    OrderStatus = a.Order != null ? a.Order.Status : string.Empty,
                    AssignmentStatus = a.Status,
                    CustomerAddress = a.Order != null && a.Order.Address != null
                        ? $"{a.Order.Address.AddressLine1}, {a.Order.Address.Area}, {a.Order.Address.City}"
                        : "No address",
                    AssignedAt = a.AssignedAt
                })
                .ToListAsync();

            Notifications = await _context.Notifications
                .Where(n =>
                    n.UserID == deliveryPerson.UserID &&
                    n.Type == "DeliveryAssignment")
                .OrderByDescending(n => n.SentAt)
                .Take(5)
                .Select(n => new DeliveryNotificationViewModel
                {
                    NotificationID = n.NotificationID,
                    Title = n.Title,
                    Message = n.Message,
                    SentAt = n.SentAt,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostStartDeliveryAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Approved delivery profile was not found.";
                return RedirectToPage();
            }

            var assignment = await _context.DeliveryAssignments
                .Include(a => a.Order)
                .FirstOrDefaultAsync(a =>
                    a.AssignmentID == assignmentId &&
                    a.DeliveryPersonID == deliveryPerson.DeliveryPersonID);

            if (assignment == null || assignment.Order == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToPage();
            }

            if (!deliveryPerson.CurrentLatitude.HasValue ||
                !deliveryPerson.CurrentLongitude.HasValue ||
                !deliveryPerson.LastLocationUpdate.HasValue)
            {
                TempData["Error"] = "Please allow GPS first. Keep the page open until your location appears, then click Start Delivery.";
                return RedirectToPage();
            }

            var lastGpsAgeSeconds =
                Math.Abs((DateTime.UtcNow - deliveryPerson.LastLocationUpdate.Value).TotalSeconds);

            if (lastGpsAgeSeconds > 60)
            {
                TempData["Error"] = "Your GPS is old. Please wait for location update, then click Start Delivery again.";
                return RedirectToPage();
            }

            assignment.Status = "OutForDelivery";
            assignment.PickupTime = DateTime.UtcNow;
            assignment.Order.Status = "Out for Delivery";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery started. Customer can now track your movement.";

            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostMarkDeliveredAsync(int assignmentId)
        {
            var assignment = await GetAssignmentForCurrentDeliveryPersonAsync(assignmentId);

            if (assignment == null || assignment.Order == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToPage();
            }

            assignment.Status = "Delivered";
            assignment.DeliveryTime = DateTime.UtcNow;
            assignment.Order.Status = "Delivered";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Order marked as delivered.";

            return RedirectToPage();
        }

        private async Task<DeliveryAssignment?> GetAssignmentForCurrentDeliveryPersonAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                return null;
            }

            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                .FirstOrDefaultAsync(a =>
                    a.AssignmentID == assignmentId &&
                    a.DeliveryPersonID == deliveryPerson.DeliveryPersonID);
        }

        private async Task<DeliveryPerson?> GetCurrentDeliveryPersonAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return null;
            }

            return await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.UserID == user.Id &&
                    d.IsActive &&
                    d.Status == "Approved");
        }
    }

    public class DeliveryAssignmentViewModel
    {
        public int AssignmentID { get; set; }

        public int OrderID { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public string OrderStatus { get; set; } = string.Empty;

        public string AssignmentStatus { get; set; } = string.Empty;

        public string CustomerAddress { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; }
    }

    public class DeliveryNotificationViewModel
    {
        public int NotificationID { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }

        public bool IsRead { get; set; }
    }
}