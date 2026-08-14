using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages.Deliverypages
{
    [Authorize(Roles = "Delivery")]
    public class DeliveryOrderDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly DeliveryManager _deliveryManager;
        private readonly MessagingManager _messagingManager;

        public DeliveryOrderDetailsModel(
            ApplicationDbContext context,
            UserManager<User> userManager,
            DeliveryManager deliveryManager,
            MessagingManager messagingManager)
        {
            _context = context;
            _userManager = userManager;
            _deliveryManager = deliveryManager;
            _messagingManager = messagingManager;
        }

        public OrderDetailsViewModel? Details { get; set; }

        public string? ErrorMessage { get; set; }

        // ==========================================
        // LOAD ORDER DETAILS
        // ==========================================
        public async Task<IActionResult> OnGetAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                ErrorMessage = "Approved delivery profile was not found.";
                return Page();
            }

            var assignment = await LoadAssignmentAsync(assignmentId, deliveryPerson.DeliveryPersonID);

            if (assignment == null || assignment.Order == null)
            {
                ErrorMessage = "This order was not found or is not assigned to you.";
                return Page();
            }

            Details = BuildViewModel(assignment);

            return Page();
        }

        // ==========================================
        // CONFIRM CASH COLLECTION
        // ==========================================
        public async Task<IActionResult> OnPostConfirmCashCollectionAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Approved delivery profile was not found.";
                return RedirectToPage(new { assignmentId });
            }

            try
            {
                var collected = await _deliveryManager.ConfirmCashCollectionAsync(
                    assignmentId,
                    deliveryPerson.DeliveryPersonID);

                TempData["Success"] = $"Cash collection confirmed (${collected:0.00}).";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { assignmentId });
        }

        // ==========================================
        // START DELIVERY / MARK DELIVERED
        // ==========================================
        public async Task<IActionResult> OnPostStartDeliveryAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Approved delivery profile was not found.";
                return RedirectToPage(new { assignmentId });
            }

            var assignment = await LoadAssignmentAsync(assignmentId, deliveryPerson.DeliveryPersonID);

            if (assignment == null || assignment.Order == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToPage(new { assignmentId });
            }

            if (!string.Equals(assignment.Status, "Assigned", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only assigned orders can be started.";
                return RedirectToPage(new { assignmentId });
            }

            assignment.Status = "OutForDelivery";
            assignment.PickupTime = DateTime.UtcNow;
            assignment.Order.Status = "Out for Delivery";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Delivery started.";
            return RedirectToPage(new { assignmentId });
        }

        public async Task<IActionResult> OnPostMarkDeliveredAsync(int assignmentId)
        {
            var deliveryPerson = await GetCurrentDeliveryPersonAsync();

            if (deliveryPerson == null)
            {
                TempData["Error"] = "Approved delivery profile was not found.";
                return RedirectToPage(new { assignmentId });
            }

            var assignment = await LoadAssignmentAsync(assignmentId, deliveryPerson.DeliveryPersonID);

            if (assignment == null || assignment.Order == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToPage(new { assignmentId });
            }

            if (!string.Equals(assignment.Status, "OutForDelivery", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "You must start delivery before marking it delivered.";
                return RedirectToPage(new { assignmentId });
            }

            var paymentMethod = assignment.Order.PaymentMethod?.Trim() ?? string.Empty;

            var isCashOnDelivery =
                paymentMethod.Equals("Cash On Delivery", StringComparison.OrdinalIgnoreCase) ||
                paymentMethod.Equals("COD", StringComparison.OrdinalIgnoreCase);

            if (isCashOnDelivery &&
                !string.Equals(assignment.Order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Please confirm cash collection before marking this order as delivered.";
                return RedirectToPage(new { assignmentId });
            }

            assignment.Status = "Delivered";
            assignment.DeliveryTime = DateTime.UtcNow;
            assignment.Order.Status = "Delivered";

            await _context.SaveChangesAsync();

            // ==========================================
            // NOTIFY CUSTOMER VIA EXISTING CHAT SYSTEM
            // (only after Delivered is saved successfully;
            // reuses the same MessagingManager.SendMessageAsync
            // call already used by ChatConversation.cshtml.cs)
            // ==========================================
            var customerUserId = assignment.Order.Customer?.User?.Id;

            // ===== TEMPORARY DEBUG - REMOVE AFTER TESTING =====
            Console.WriteLine("[MARK-DELIVERED-DEBUG] Reached messaging block.");
            Console.WriteLine($"[MARK-DELIVERED-DEBUG] deliveryPerson.UserID = {deliveryPerson.UserID}");
            Console.WriteLine($"[MARK-DELIVERED-DEBUG] customerUserId = {(customerUserId.HasValue ? customerUserId.Value.ToString() : "NULL")}");
            Console.WriteLine($"[MARK-DELIVERED-DEBUG] OrderNumber = {assignment.Order.OrderNumber}");
            // ===== END TEMPORARY DEBUG =====

            if (customerUserId.HasValue && customerUserId.Value > 0)
            {
                // ===== TEMPORARY DEBUG - REMOVE AFTER TESTING =====
                Console.WriteLine("[MARK-DELIVERED-DEBUG] Entering SendMessageAsync call now...");
                try
                {
                    var sentMessageId = await _messagingManager.SendMessageAsync(new ChatMessageDTO
                    {
                        SenderID = deliveryPerson.UserID,
                        ReceiverID = customerUserId.Value,
                        MessageText = $"Your order {assignment.Order.OrderNumber} has been delivered successfully."
                    }, "", "");

                    Console.WriteLine($"[MARK-DELIVERED-DEBUG] SendMessageAsync SUCCEEDED. MessageID = {sentMessageId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MARK-DELIVERED-DEBUG] SendMessageAsync THREW AN EXCEPTION:");
                    Console.WriteLine(ex.ToString());
                    throw;
                }
                // ===== END TEMPORARY DEBUG =====
            }
            else
            {
                // ===== TEMPORARY DEBUG - REMOVE AFTER TESTING =====
                Console.WriteLine("[MARK-DELIVERED-DEBUG] SKIPPED SendMessageAsync — customerUserId was null or <= 0.");
                // ===== END TEMPORARY DEBUG =====
            }

            TempData["Success"] = "Order marked as delivered.";
            return RedirectToPage(new { assignmentId });
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private async Task<DeliveryAssignment?> LoadAssignmentAsync(int assignmentId, int deliveryPersonId)
        {
            return await _context.DeliveryAssignments
                .Include(a => a.Order)
                    .ThenInclude(o => o!.Address)
                .Include(a => a.Order)
                    .ThenInclude(o => o!.Customer)
                        .ThenInclude(c => c.User)
                .Include(a => a.Order)
                    .ThenInclude(o => o!.OrderItems)
                        .ThenInclude(i => i.Store)
                .Include(a => a.Order)
                    .ThenInclude(o => o!.Payments)
                .FirstOrDefaultAsync(a =>
                    a.AssignmentID == assignmentId &&
                    a.DeliveryPersonID == deliveryPersonId);
        }

        private static OrderDetailsViewModel BuildViewModel(DeliveryAssignment assignment)
        {
            var order = assignment.Order!;

            var paymentMethod = order.PaymentMethod ?? string.Empty;

            var isCod =
                paymentMethod.Trim().Equals("Cash On Delivery", StringComparison.OrdinalIgnoreCase) ||
                paymentMethod.Trim().Equals("COD", StringComparison.OrdinalIgnoreCase);

            return new OrderDetailsViewModel
            {
                AssignmentID = assignment.AssignmentID,
                OrderID = order.OrderID,
                OrderNumber = order.OrderNumber,
                OrderStatus = order.Status,
                AssignmentStatus = assignment.Status,

                CustomerName = order.Customer?.User?.FullName ?? "N/A",
                CustomerPhone = order.Customer?.User?.PhoneNumber ?? "N/A",

                AddressLine1 = order.Address?.AddressLine1 ?? string.Empty,
                Area = order.Address?.Area ?? string.Empty,
                City = order.Address?.City ?? string.Empty,

                Products = order.OrderItems
                    .Select(i => new ProductLineViewModel
                    {
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        ProductPrice = i.ProductPrice,
                        TotalPrice = i.TotalPrice,
                        StoreName = i.Store?.StoreName ?? "N/A"
                    })
                    .ToList(),

                StoreNames = string.Join(", ",
                    order.OrderItems
                        .Where(i => i.Store != null)
                        .Select(i => i.Store.StoreName)
                        .Distinct()),

                Subtotal = order.Subtotal,
                DeliveryFee = order.DeliveryFee,
                DiscountAmount = order.DiscountAmount,
                TaxAmount = order.TaxAmount,
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,

                PaymentMethod = string.IsNullOrWhiteSpace(order.PaymentMethod) ? "N/A" : order.PaymentMethod,
                PaymentStatus = string.IsNullOrWhiteSpace(order.PaymentStatus) ? "N/A" : order.PaymentStatus,

                IsCashOnDelivery = isCod,

                NeedsCashCollection =
                    isCod &&
                    !order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase),

                // "Your Earnings" reuses the existing Order.DeliveryFee
                // value already shown above as "Delivery Fee" â€” no new
                // calculation. Only shown once the assignment is
                // actually Delivered, matching the Dashboard's
                // Total Earnings KPI (Delivered-only).
                IsDelivered = string.Equals(
                    assignment.Status, "Delivered", StringComparison.OrdinalIgnoreCase),

                YourEarnings = order.DeliveryFee
            };
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

    // ==========================================
    // VIEW MODELS
    // ==========================================
    public class OrderDetailsViewModel
    {
        public int AssignmentID { get; set; }
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string AssignmentStatus { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public string AddressLine1 { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        public string FullAddress =>
            string.Join(", ", new[] { AddressLine1, Area, City }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

        public List<ProductLineViewModel> Products { get; set; } = new();
        public string StoreNames { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;

        public bool IsCashOnDelivery { get; set; }
        public bool NeedsCashCollection { get; set; }

        public bool IsDelivered { get; set; }
        public decimal YourEarnings { get; set; }
    }

    public class ProductLineViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string StoreName { get; set; } = string.Empty;
    }
}
