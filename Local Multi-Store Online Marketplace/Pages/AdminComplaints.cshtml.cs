using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminComplaintsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ComplaintManager _complaintManager;
        private readonly ILogger<AdminComplaintsModel> _logger;

        public AdminComplaintsModel(
            ApplicationDbContext context,
            ComplaintManager complaintManager,
            ILogger<AdminComplaintsModel> logger)
        {
            _context = context;
            _complaintManager = complaintManager;
            _logger = logger;
        }

        public List<AdminComplaintViewModel> Complaints { get; set; }
            = new();

        public async Task OnGetAsync()
        {
            await LoadComplaintsAsync();
        }

        // =====================================================
        // RESOLVE COMPLAINT
        // =====================================================
        public async Task<IActionResult> OnPostResolveAsync(
            int complaintId)
        {
            if (complaintId <= 0)
            {
                TempData["Error"] =
                    "Invalid complaint ID.";

                return RedirectToPage();
            }

            try
            {
                var complaint =
                    await _context.Complaints
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c =>
                            c.ComplaintID == complaintId);

                if (complaint == null)
                {
                    TempData["Error"] =
                        "Complaint not found.";

                    return RedirectToPage();
                }

                if (string.Equals(
                        complaint.Status,
                        "Resolved",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Info"] =
                        $"Complaint #{complaintId} is already resolved.";

                    return RedirectToPage();
                }

                var ipAddress =
                    HttpContext.Connection.RemoteIpAddress?
                        .ToString()
                    ?? "Unknown";

                var userAgent =
                    Request.Headers.UserAgent.ToString();

                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = "Unknown";
                }

                await _complaintManager.ResolveComplaintAsync(
                    complaintId,
                    "Complaint reviewed and resolved by the administrator.",
                    "Resolved from the Admin Complaints page.",
                    ipAddress,
                    userAgent);

                TempData["Success"] =
                    $"Complaint #{complaintId} was resolved successfully.";
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error resolving complaint {ComplaintId}.",
                    complaintId);

                TempData["Error"] =
                    "The complaint could not be resolved. Please try again.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // REFUND COMPLAINT
        // =====================================================
        public async Task<IActionResult> OnPostRefundAsync(
            int complaintId)
        {
            if (complaintId <= 0)
            {
                TempData["Error"] =
                    "Invalid complaint ID.";

                return RedirectToPage();
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var complaint =
                    await _context.Complaints
                        .Include(c => c.Order)
                        .FirstOrDefaultAsync(c =>
                            c.ComplaintID == complaintId);

                if (complaint == null)
                {
                    TempData["Error"] =
                        "Complaint not found.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                if (!complaint.OrderID.HasValue)
                {
                    TempData["Error"] =
                        "This complaint is not connected to an order, so a refund cannot be issued.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                var payment =
                    await _context.Payments
                        .FirstOrDefaultAsync(p =>
                            p.OrderID == complaint.OrderID.Value);

                if (payment == null)
                {
                    TempData["Error"] =
                        "No payment record was found for this complaint's order.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                if (string.Equals(
                        payment.Status,
                        "Refunded",
                        StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Info"] =
                        $"The payment for complaint #{complaintId} has already been refunded.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                if (payment.Amount <= 0)
                {
                    TempData["Error"] =
                        "The payment amount is invalid and cannot be refunded.";

                    await transaction.RollbackAsync();

                    return RedirectToPage();
                }

                var ipAddress =
                    HttpContext.Connection.RemoteIpAddress?
                        .ToString()
                    ?? "Unknown";

                var userAgent =
                    Request.Headers.UserAgent.ToString();

                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = "Unknown";
                }

                // Update payment refund information.
                payment.Status = "Refunded";
                payment.RefundAmount = payment.Amount;
                payment.RefundDate = DateTime.UtcNow;

                // Keep the order payment status synchronized.
                if (complaint.Order != null)
                {
                    complaint.Order.PaymentStatus = "Refunded";
                }

                await _context.SaveChangesAsync();

                // Resolve the complaint using the existing manager,
                // which also creates the audit log.
                await _complaintManager.ResolveComplaintAsync(
                    complaintId,
                    $"Full refund of ${payment.Amount:N2} was issued.",
                    "Complaint resolved through a full payment refund.",
                    ipAddress,
                    userAgent);

                await transaction.CommitAsync();

                TempData["Success"] =
                    $"A refund of ${payment.Amount:N2} was issued successfully for complaint #{complaintId}.";
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();

                _logger.LogError(
                    exception,
                    "Error refunding complaint {ComplaintId}.",
                    complaintId);

                TempData["Error"] =
                    "The refund could not be completed. No changes were saved.";
            }

            return RedirectToPage();
        }

        // =====================================================
        // LOAD COMPLAINTS FROM DATABASE
        // =====================================================
        private async Task LoadComplaintsAsync()
        {
            Complaints =
                await _context.Complaints
                    .AsNoTracking()
                    .Include(c => c.Store)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new AdminComplaintViewModel
                    {
                        ComplaintId =
                            c.ComplaintID,

                        CustomerName =
                            "Customer #" + c.CustomerID,

                        StoreName =
                            c.Store != null
                                ? c.Store.StoreName
                                : c.StoreID.HasValue
                                    ? "Store #" + c.StoreID.Value
                                    : "General Complaint",

                        Type =
                            c.ComplaintType,

                        Description =
                            c.Description,

                        Status =
                            string.IsNullOrWhiteSpace(c.Status)
                                ? "Pending"
                                : c.Status,

                        CreatedAt =
                            c.CreatedAt,

                        OrderId =
                            c.OrderID,

                        Resolution =
                            c.Resolution,

                        ResolvedAt =
                            c.ResolvedAt
                    })
                    .ToListAsync();
        }
    }

    public class AdminComplaintViewModel
    {
        public int ComplaintId { get; set; }

        public string CustomerName { get; set; }
            = string.Empty;

        public string StoreName { get; set; }
            = string.Empty;

        public string Type { get; set; }
            = string.Empty;

        public string Description { get; set; }
            = string.Empty;

        public string Status { get; set; }
            = string.Empty;

        public DateTime CreatedAt { get; set; }

        public int? OrderId { get; set; }

        public string? Resolution { get; set; }

        public DateTime? ResolvedAt { get; set; }
    }
}

