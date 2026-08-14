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

        // NOTE: "Promotion" notifications are customer-only (they deep-link
        // to /CustomerPromotions, a customer page). This full list is the
        // base set; GetVisibleTypesFor(...) below includes "Promotion" only
        // for customers, so it never shows up — or opens — for store owners
        // or delivery accounts. See the bugfix note on GetVisibleTypesFor.
        private static readonly string[] AllNotificationTypes =
        {
            "ReportUpdate",
            "AdminWarning",
            "OrderStatus",      // BUGFIX: was "OrderUpdate" — IndexModel.OnPostUpdateStatusAsync
                    "NewOrder",                // actually writes Type = "OrderStatus", so this never matched
                                 // and order-status notifications never showed up here.
            "PaymentUpdate",
            "DeliveryRequest",
            "DeliveryAssignment",       // NEW — order assigned to a delivery person (AdminAssignDelivery.cshtml.cs)
            "DeliveryReview",           // NEW — customer rated a completed delivery (DeliveryReviewManager.cs)
            "DeliveryReadyForPickup",   // NEW — store finished preparing the order (Details.cshtml.cs)
            "ComplaintUpdate",         // NEW — a customer's filed complaint was resolved/refunded (AdminComplaintsModel.cs)
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

        // Notification types that deep-link to a specific order row on
        // CustomerOrders (ReferenceID must hold that order's OrderID).
        // NOTE: "NewOrder" is the store-owner-side notification ("you got
        // a new order") — it routes to the StoreOwner Orders index instead,
        // handled separately below.
        private static readonly string[] OrderLinkedTypes =
        {
            "OrderStatus"
        };

        public ReportUpdatesModel(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<Notification> NotificationsList { get; set; } = new();

        public bool IsStoreOwner { get; set; }
        public bool IsDeliveryPerson { get; set; }

        // BUGFIX — "Promotion" is a customer-only notification (it deep-links
        // to /CustomerPromotions). It's now included only when the viewer is
        // a customer — not just "not a store owner" — so delivery accounts
        // are covered by the same rule instead of only store owners.
        private static List<string> GetVisibleTypesFor(bool isCustomer)
        {
            return isCustomer
                ? AllNotificationTypes.ToList()
                : AllNotificationTypes.Where(t => t != "Promotion").ToList();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsStoreOwner = await _userManager.IsInRoleAsync(user, "StoreOwner");
            IsDeliveryPerson = await _userManager.IsInRoleAsync(user, "Delivery");

            var isCustomer = !IsStoreOwner && !IsDeliveryPerson;
            var visibleTypes = GetVisibleTypesFor(isCustomer);

            NotificationsList = await _context.Notifications
                .Where(n => n.UserID == user.Id && visibleTypes.Contains(n.Type))
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostOpenNotificationAsync(int notificationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var isStoreOwner = await _userManager.IsInRoleAsync(user, "StoreOwner");
            var isDeliveryPerson = await _userManager.IsInRoleAsync(user, "Delivery");
            var isCustomer = !isStoreOwner && !isDeliveryPerson;

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == user.Id);

            if (notification == null)
                return RedirectToPage();

            // Defensive guard — a store owner or delivery account should
            // never be able to open a "Promotion" notification (it's
            // filtered out of their list on OnGetAsync already, but a
            // forged/replayed form post could still hit this handler
            // directly). Mark it read and send them to their own home page
            // instead of following the customer-only redirect below.
            if (!isCustomer && notification.Type == "Promotion")
            {
                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    await _context.SaveChangesAsync();
                }

                return RedirectToPage(isStoreOwner ? "/StoreOwner/Home" : "/DeliveryProfile");
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            // Deep-link straight to the record this notification is about,
            // instead of just re-showing the notification list.
            if (notification.ReferenceID.HasValue)
            {
                var refId = notification.ReferenceID.Value;

                // Customer side: order status changed -> customer's own orders page
                if (OrderLinkedTypes.Contains(notification.Type))
                {
                    return RedirectToPage("/CustomerOrders", new { highlightOrderId = refId });
                }

                // Store-owner side: "you got a new order" -> Orders index,
                // with that row scrolled to and highlighted.
                if (notification.Type == "NewOrder")
                {
                    return RedirectToPage("/StoreOwner/Order/Index", new { highlightOrderId = refId });
                }

                if (notification.Type == "Promotion")
                {
                    return RedirectToPage("/CustomerPromotions", new { highlightPromoId = refId });
                }

                // Story like / story reply -> Home page, open that story.
                // Assumes ReferenceID holds the StoryID.
                if (notification.Type == "StoryLike" || notification.Type == "StoryReply")
                {
                    return RedirectToPage("/StoreOwner/Home", new { openStoryId = refId });
                }

                // Follow -> Home page, highlight the followers stat.
                if (notification.Type == "Follow")
                {
                    return RedirectToPage("/StoreOwner/Home", new { highlightFollowers = true });
                }

                // Product review -> Home page, open that exact product
                // and highlight the specific review inside it.
                // Assumes ReferenceID holds Review.ReviewID and Review has a ProductID FK.
                if (notification.Type == "ProductReview")
                {
                    var productId = await _context.Reviews
                        .Where(r => r.ReviewID == refId)
                        .Select(r => (int?)r.ProductID)
                        .FirstOrDefaultAsync();

                    if (productId.HasValue)
                    {
                        return RedirectToPage("/StoreOwner/Home", new
                        {
                            productId = productId.Value,
                            highlightReviewId = refId
                        });
                    }
                }

                // Store review -> /StoreReviews/{storeId}, scrolled to and
                // highlighted via the page's own #review-<id> hash handling
                // (see the DOMContentLoaded script at the bottom of that page).
                // Same Reviews table as ProductReview above — a store review is
                // just a row with StoreID set (and ProductID null).
                if (notification.Type == "StoreReview")
                {
                    var storeId = await _context.Reviews
                        .Where(r => r.ReviewID == refId)
                        .Select(r => (int?)r.StoreID)
                        .FirstOrDefaultAsync();

                    if (storeId.HasValue)
                    {
                        return RedirectToPage("/StoreReviews", null, new { storeId = storeId.Value }, "review-" + refId);
                    }
                }
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

        // NEW — "Mark all as read", scoped to whatever this role is allowed
        // to see (so it can't quietly mark a store owner's hidden Promotion
        // rows as read either — not that they'd see them to begin with).
        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var isStoreOwner = await _userManager.IsInRoleAsync(user, "StoreOwner");
            var isDeliveryPerson = await _userManager.IsInRoleAsync(user, "Delivery");
            var isCustomer = !isStoreOwner && !isDeliveryPerson;
            var visibleTypes = GetVisibleTypesFor(isCustomer);

            var unread = await _context.Notifications
                .Where(n => n.UserID == user.Id && !n.IsRead && visibleTypes.Contains(n.Type))
                .ToListAsync();

            if (unread.Any())
            {
                foreach (var n in unread)
                    n.IsRead = true;

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
                "AccountStatement" => ("fas fa-file-invoice-dollar", "type-account"),
                "OrderStatus" => ("fas fa-box", "type-order"),
                "NewOrder" => ("fas fa-box", "type-order"),
                "PaymentUpdate" => ("fas fa-credit-card", "type-account"),
                "DeliveryRequest" => ("fas fa-truck", "type-order"),
                "DeliveryAssignment" => ("fas fa-truck", "type-order"),
                "DeliveryReview" => ("fas fa-star", "type-review"),
                "DeliveryReadyForPickup" => ("fas fa-box-open", "type-order"),
                "ComplaintUpdate" => ("fas fa-flag-checkered", "type-review"),
                "Promotion" => ("fas fa-bullhorn", "type-promotion"),
                "StoreRequest" => ("fas fa-store", "type-order"),
                "AdminWarning" => ("fas fa-triangle-exclamation", "type-warning"),
                "ReportUpdate" => ("fas fa-flag", "type-warning"),
                _ => ("fas fa-bell", "")
            };
        }
    }
}