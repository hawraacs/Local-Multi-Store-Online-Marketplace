using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAuditLogsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AdminAuditLogsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<AuditLogDto> Logs { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Most-recent-first. Capped at 1000 rows for now since the page does its
            // filtering/paging client-side — if the table grows large, this should move
            // to real server-side paging (Skip/Take driven by query-string parameters)
            // instead of raising this cap.
            Logs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.ActionDate)
                .Take(1000)
                .Select(a => new AuditLogDto
                {
                    Timestamp = a.ActionDate,
                    UserName = a.User != null
                        ? (a.User.UserName ?? a.User.Email ?? "Unknown")
                        : "Unknown",
                    Action = a.Action,
                    EntityName = a.EntityName,
                    IPAddress = a.IPAddress
                })
                .ToListAsync();
        }
    }

    public class AuditLogDto
    {
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
    }
}
