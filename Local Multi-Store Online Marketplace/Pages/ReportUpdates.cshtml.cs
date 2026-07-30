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

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    [Authorize(Roles = "Customer,StoreOwner,Delivery")]
    public class ReportUpdatesModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        private static readonly string[] VisibleNotificationTypes =
        {
            "ReportUpdate",
            "AdminWarning",
            "OrderStatus",      // BUGFIX: was "OrderUpdate" — IndexModel.OnPostUpdateStatusAsync
                    "NewOrder",                // actually writes Type = "OrderStatus", so this never matched
                                 // and order-status notifications never showed up here.
            "PaymentUpdate",
            "DeliveryRequest",
            "Promotion",
            "StoreRequest",

            // NEW — store-owner side notification types
            "Like",             // customer liked store/product content
            "Comment",          // customer commented
            "ProductReview",    // new review on a product
            "StoreReview",      // new review on a store
            "StoryLike",        // customer liked a story
            "StoryReply",       // customer replied to a story
            "AccountStatement", // subscription/boost/payment processed
            "Follow"            // customer followed the store
        };

        public ReportUpdatesModel(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<Notification> NotificationsList { get; set; } = new();

        public bool IsStoreOwner { get; set; }
        public bool IsDeliveryPerson { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsStoreOwner = await _userManager.IsInRoleAsync(user, "StoreOwner");
            IsDeliveryPerson = await _userManager.IsInRoleAsync(user, "Delivery");

            NotificationsList = await _context.Notifications
                .Where(n => n.UserID == user.Id && VisibleNotificationTypes.Contains(n.Type))
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

        public async Task<IActionResult> OnPostDeleteNotificationAsync(int notificationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == user.Id);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        // Maps a Notification.Type to the icon (Font Awesome class) and
        // color-tint CSS class used in the store-owner notification list,
        // so replies, likes, comments, etc. are visually distinguishable
        // instead of all sharing the same generic flag icon.
        public static (string IconClass, string CssClass) GetNotificationIcon(string type)
        {
            return type switch
            {
                "Like" => ("fas fa-heart", "type-like"),
                "Comment" => ("fas fa-comment", "type-comment"),
                "ProductReview" => ("fas fa-star", "type-review"),
                "StoreReview" => ("fas fa-star", "type-review"),
                "StoryLike" => ("fas fa-circle-play", "type-story-like"),
                "StoryReply" => ("fas fa-reply", "type-story-reply"),
                "Follow" => ("fas fa-user-plus", "type-follow"),
                "AccountStatement" => ("fas fa-file-invoice-dollar", ""),
                "OrderStatus" => ("fas fa-box", ""),
                "NewOrder" => ("fas fa-box", ""),
                "PaymentUpdate" => ("fas fa-credit-card", ""),
                "DeliveryRequest" => ("fas fa-truck", ""),
                "Promotion" => ("fas fa-bullhorn", ""),
                "StoreRequest" => ("fas fa-store", ""),
                "AdminWarning" => ("fas fa-triangle-exclamation", ""),
                "ReportUpdate" => ("fas fa-flag", ""),
                _ => ("fas fa-bell", "")
            };
        }
    }
}