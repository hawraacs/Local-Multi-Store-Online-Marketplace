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
    [Authorize(Roles = "Admin")]
    public class AdminReportsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationManager _notifications;
        private readonly UserManager<User> _userManager;

        public AdminReportsModel(ApplicationDbContext context, NotificationManager notifications, UserManager<User> userManager)
        {
            _context = context;
            _notifications = notifications;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "Pending Review";

        // Deep-link target from a notification (?id=123). When present, the
        // default "Pending Review" tab is overridden to "All" below so an
        // already-resolved/dismissed report isn't hidden from view.
        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public List<ReportRow> Reports { get; set; } = new();

        public int PendingCount { get; set; }
        public int ResolvedCount { get; set; }
        public int DismissedCount { get; set; }

        public async Task OnGetAsync()
        {
            // If we're deep-linking to a specific report and the caller didn't
            // explicitly choose a tab, show "All" so the target report is
            // guaranteed to be in the filtered list regardless of its status.
            if (Id.HasValue && !Request.Query.ContainsKey("StatusFilter"))
            {
                StatusFilter = "All";
            }

            var reports = await _context.Reports
                .Include(r => r.ReporterCustomer)
                    .ThenInclude(cu => cu!.User)
                .Include(r => r.ReporterStore)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            PendingCount = reports.Count(r => r.Status == "Pending Review");
            ResolvedCount = reports.Count(r => r.Status == "Resolved");
            DismissedCount = reports.Count(r => r.Status == "Dismissed");

            var filtered = string.Equals(StatusFilter, "All", StringComparison.OrdinalIgnoreCase)
                ? reports
                : reports.Where(r => r.Status == StatusFilter).ToList();

            var rows = new List<ReportRow>();
            foreach (var r in filtered)
            {
                rows.Add(await BuildRowAsync(r));
            }
            Reports = rows;
        }

        public async Task<IActionResult> OnPostResolveAsync(int reportId, string? adminNotes)
        {
            await SetStatusAsync(reportId, "Resolved", adminNotes);
            TempData["Success"] = "Report marked as resolved.";
            return RedirectToPage(new { StatusFilter });
        }

        public async Task<IActionResult> OnPostDismissAsync(int reportId, string? adminNotes)
        {
            await SetStatusAsync(reportId, "Dismissed", adminNotes);
            TempData["Success"] = "Report dismissed.";
            return RedirectToPage(new { StatusFilter });
        }

        public async Task<IActionResult> OnPostReopenAsync(int reportId)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.ReportID == reportId);
            if (report != null)
            {
                report.Status = "Pending Review";
                report.ResolvedAt = null;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Report reopened for review.";
            return RedirectToPage(new { StatusFilter });
        }

        // =====================================================
        // WARN REPORTED USER (does not close the report)
        // =====================================================
        public async Task<IActionResult> OnPostWarnAsync(int reportId, string? warningMessage)
        {
            if (string.IsNullOrWhiteSpace(warningMessage))
            {
                TempData["Error"] = "Please enter a warning message before sending.";
                return RedirectToPage(new { StatusFilter });
            }

            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.ReportID == reportId);

            if (report == null)
            {
                TempData["Error"] = "Report not found.";
                return RedirectToPage(new { StatusFilter });
            }

            var targetUserId = await GetTargetUserIdAsync(report);
            if (!targetUserId.HasValue)
            {
                TempData["Error"] = "Could not determine who to warn for this report.";
                return RedirectToPage(new { StatusFilter });
            }

            var trimmedMessage = warningMessage.Trim();

            await _notifications.SendAsync(
                userId: targetUserId.Value,
                title: "Warning from Marketplace Admin",
                message: trimmedMessage,
                type: "AdminWarning",
                referenceId: report.ReportID,
                sentVia: "System");

            // Report stays open (Status/ResolvedAt untouched); the note is logged
            // so other admins can see a warning was already sent for this report.
            var stamp = $"[Warning sent {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC] {trimmedMessage}";
            report.AdminNotes = string.IsNullOrWhiteSpace(report.AdminNotes)
                ? stamp
                : $"{report.AdminNotes}\n{stamp}";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Warning sent to the reported user.";
            return RedirectToPage(new { StatusFilter });
        }

        // =====================================================
        // DEACTIVATE REPORTED USER (Instagram-style suspension)
        // =====================================================
        public async Task<IActionResult> OnPostDeactivateUserAsync(int reportId)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.ReportID == reportId);
            if (report == null)
            {
                TempData["Error"] = "Report not found.";
                return RedirectToPage(new { StatusFilter });
            }

            var targetUserId = await GetTargetUserIdAsync(report);
            if (!targetUserId.HasValue)
            {
                TempData["Error"] = "Could not determine which account to deactivate.";
                return RedirectToPage(new { StatusFilter });
            }

            var user = await _context.Users.FindAsync(targetUserId.Value);
            if (user == null)
            {
                TempData["Error"] = "User account not found.";
                return RedirectToPage(new { StatusFilter });
            }

            if (!user.IsActive)
            {
                TempData["Error"] = "This account is already deactivated.";
                return RedirectToPage(new { StatusFilter });
            }

            user.IsActive = false;

            var stamp = $"[Account deactivated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC]";
            report.AdminNotes = string.IsNullOrWhiteSpace(report.AdminNotes)
                ? stamp
                : $"{report.AdminNotes}\n{stamp}";

            await _context.SaveChangesAsync();

            await _notifications.SendAsync(
                userId: user.Id,
                title: "Your account has been deactivated",
                message: "Your account was deactivated by our team following a report review. Contact support if you believe this is a mistake.",
                type: "AccountDeactivated",
                referenceId: report.ReportID,
                sentVia: "System");

            TempData["Success"] = "Account deactivated.";
            return RedirectToPage(new { StatusFilter });
        }

        public async Task<IActionResult> OnPostReactivateUserAsync(int reportId)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.ReportID == reportId);
            if (report == null)
            {
                TempData["Error"] = "Report not found.";
                return RedirectToPage(new { StatusFilter });
            }

            var targetUserId = await GetTargetUserIdAsync(report);
            if (!targetUserId.HasValue)
            {
                TempData["Error"] = "Could not determine which account to reactivate.";
                return RedirectToPage(new { StatusFilter });
            }

            var user = await _context.Users.FindAsync(targetUserId.Value);
            if (user == null)
            {
                TempData["Error"] = "User account not found.";
                return RedirectToPage(new { StatusFilter });
            }

            user.IsActive = true;

            var stamp = $"[Account reactivated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC]";
            report.AdminNotes = string.IsNullOrWhiteSpace(report.AdminNotes)
                ? stamp
                : $"{report.AdminNotes}\n{stamp}";

            await _context.SaveChangesAsync();

            await _notifications.SendAsync(
                userId: user.Id,
                title: "Your account has been reactivated",
                message: "Your account is active again.",
                type: "AccountReactivated",
                referenceId: report.ReportID,
                sentVia: "System");

            TempData["Success"] = "Account reactivated.";
            return RedirectToPage(new { StatusFilter });
        }

        // =====================================================
        // PERMANENTLY DELETE REPORTED USER
        // =====================================================
        public async Task<IActionResult> OnPostDeleteUserAsync(int reportId)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.ReportID == reportId);
            if (report == null)
            {
                TempData["Error"] = "Report not found.";
                return RedirectToPage(new { StatusFilter });
            }

            var targetUserId = await GetTargetUserIdAsync(report);
            if (!targetUserId.HasValue)
            {
                TempData["Error"] = "Could not determine which account to delete.";
                return RedirectToPage(new { StatusFilter });
            }

            var user = await _userManager.FindByIdAsync(targetUserId.Value.ToString());
            if (user == null)
            {
                TempData["Error"] = "User account not found.";
                return RedirectToPage(new { StatusFilter });
            }

            try
            {
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Could not delete account: " + string.Join(" ", result.Errors.Select(e => e.Description));
                    return RedirectToPage(new { StatusFilter });
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "This account can't be deleted because it still has linked stores, products, or orders. Deactivate it instead.";
                return RedirectToPage(new { StatusFilter });
            }

            var stamp = $"[Account permanently deleted {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC]";
            report.AdminNotes = string.IsNullOrWhiteSpace(report.AdminNotes)
                ? stamp
                : $"{report.AdminNotes}\n{stamp}";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Account permanently deleted.";
            return RedirectToPage(new { StatusFilter });
        }

        // Resolves which User account owns the thing that was reported (Product -> Store owner,
        // Store -> owner, Customer -> that customer's user account).
        private async Task<int?> GetTargetUserIdAsync(Report r)
        {
            switch (r.TargetType)
            {
                case "Product":
                    var product = await _context.Products
                        .Include(p => p.Store)
                        .FirstOrDefaultAsync(p => p.ProductID == r.TargetId);
                    return product?.Store?.OwnerUserID;

                case "Store":
                    var store = await _context.Stores.FindAsync(r.TargetId);
                    return store?.OwnerUserID;

                case "Customer":
                    var customer = await _context.Customers.FindAsync(r.TargetId);
                    return customer?.UserID;

                case "DeliveryPerson":
                    var deliveryPerson = await _context.DeliveryPersons.FindAsync(r.TargetId);
                    return deliveryPerson?.UserID;

                default:
                    return null;
            }
        }

        private async Task SetStatusAsync(int reportId, string status, string? adminNotes)
        {
            var report = await _context.Reports
                .Include(r => r.ReporterCustomer)
                .Include(r => r.ReporterStore)
                .FirstOrDefaultAsync(r => r.ReportID == reportId);

            if (report == null)
                return;

            report.Status = status;
            report.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? report.AdminNotes : adminNotes.Trim();
            report.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Figure out which User account filed this report
            int? recipientUserId = report.ReporterCustomer?.UserID
                ?? report.ReporterStore?.OwnerUserID;

            if (recipientUserId.HasValue)
            {
                var verb = status == "Resolved" ? "reviewed and resolved" : "reviewed and dismissed";
                var message = $"Your report about \"{report.Reason}\" was {verb} by our team." +
                              (string.IsNullOrWhiteSpace(report.AdminNotes) ? "" : $" Note: {report.AdminNotes}");

                await _notifications.SendAsync(
                    userId: recipientUserId.Value,
                    title: $"Your report was {status.ToLower()}",
                    message: message,
                    type: "ReportUpdate",
                    referenceId: report.ReportID,
                    sentVia: "System");
            }
        }

        private async Task<ReportRow> BuildRowAsync(Report r)
        {
            var reporterLabel = r.ReporterCustomer?.User?.FullName
                ?? r.ReporterCustomer?.User?.UserName
                ?? r.ReporterStore?.StoreName
                ?? "Unknown";

            string targetLabel = r.TargetType switch
            {
                "Product" => (await _context.Products.FindAsync(r.TargetId))?.ProductName ?? "Unknown Product",
                "Store" => (await _context.Stores.FindAsync(r.TargetId))?.StoreName ?? "Unknown Store",
                "Customer" => (await _context.Customers
                        .Include(c => c.User)
                        .FirstOrDefaultAsync(c => c.CustomerID == r.TargetId))?.User?.FullName
                    ?? "Unknown Customer",
                _ => "Unknown"
            };

            var targetUserId = await GetTargetUserIdAsync(r);
            bool? targetUserIsActive = null;
            if (targetUserId.HasValue)
            {
                targetUserIsActive = (await _context.Users.FindAsync(targetUserId.Value))?.IsActive;
            }

            return new ReportRow
            {
                ReportID = r.ReportID,
                Reason = string.IsNullOrWhiteSpace(r.Reason) ? "Report" : r.Reason,
                ReporterLabel = reporterLabel,
                TargetLabel = targetLabel,
                TargetType = r.TargetType,
                Description = r.Description ?? string.Empty,
                Status = string.IsNullOrWhiteSpace(r.Status) ? "Pending Review" : r.Status,
                AdminNotes = r.AdminNotes,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt,
                TargetUserId = targetUserId,
                TargetUserIsActive = targetUserIsActive
            };
        }
    }

    public class ReportRow
    {
        public int ReportID { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ReporterLabel { get; set; } = string.Empty;
        public string TargetLabel { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? TargetUserId { get; set; }
        public bool? TargetUserIsActive { get; set; }
    }
}