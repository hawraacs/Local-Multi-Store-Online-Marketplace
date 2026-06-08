using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminComplaintsModel : PageModel
    {
        // Inject your complaint service
        // private readonly IComplaintService _complaintService;

        public List<ComplaintDto> Complaints { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Replace with real data
            Complaints = new List<ComplaintDto>
            {
                new() { ComplaintId = 1, CustomerName = "John Doe", StoreName = "Fresh Mart", Type = "Wrong Item", Status = "Pending", CreatedAt = DateTime.Now.AddDays(-1) },
                new() { ComplaintId = 2, CustomerName = "Alice", StoreName = "Tech Zone", Type = "Damaged", Status = "Resolved", CreatedAt = DateTime.Now.AddDays(-3) }
            };
        }
    }

    public class ComplaintDto
    {
        public int ComplaintId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}