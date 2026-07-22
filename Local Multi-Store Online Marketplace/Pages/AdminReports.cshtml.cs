using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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

        public AdminReportsModel(ApplicationDbContext context, NotificationManager notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "Pending Review";

        public List<ReportRow> Reports { get; set; } = new();

        public int PendingCount { get; set; }
        public int ResolvedCount { get; set; }
        public int DismissedCount { get; set; }

        public async Task OnGetAsync()
        {
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
                ResolvedAt = r.ResolvedAt
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
    }
}
