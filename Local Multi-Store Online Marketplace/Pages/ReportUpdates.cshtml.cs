using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer,StoreOwner")]
    public class ReportUpdatesModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public ReportUpdatesModel(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<Notification> NotificationsList { get; set; } = new();

        // Tells the view which role's chrome/back-link to render
        public bool IsStoreOwner { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsStoreOwner = await _userManager.IsInRoleAsync(user, "StoreOwner");

            NotificationsList = await _context.Notifications
                .Where(n => n.UserID == user.Id && n.Type == "ReportUpdate")
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostOpenNotificationAsync(int notificationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == user.Id);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}