using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAuditLogsModel : PageModel
    {
        // Inject your audit service
        public List<AuditLogDto> Logs { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Replace with real data
            Logs = new List<AuditLogDto>
            {
                new() { Timestamp = DateTime.Now.AddHours(-1), UserName = "Admin", Action = "Approved Store", EntityName = "Store", IPAddress = "192.168.1.1" },
                new() { Timestamp = DateTime.Now.AddHours(-2), UserName = "John", Action = "Login", EntityName = "User", IPAddress = "10.0.0.1" }
            };
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