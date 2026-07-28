using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class ChatModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly MessagingManager _messagingManager;
        private readonly SessionManager _sessionManager;
        private readonly IStoreRepository _storeRepository;

        public ChatModel(
            UserManager<User> userManager,
            MessagingManager messagingManager,
            SessionManager sessionManager,
            IStoreRepository storeRepository)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
            _sessionManager = sessionManager;
            _storeRepository = storeRepository;
        }

        public int CurrentUserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchText { get; set; } = "";

        // Changed from List<User> to a lightweight view model so the
        // Razor page can keep using "user.Id" / "user.FullName" exactly
        // as before, but FullName now holds the correct role-based
        // display name (store name / delivery full name / user full name).
        public List<UserSearchResult> SearchUsers { get; set; } = new();
        public List<InboxItem> Inbox { get; set; } = new();
        public int TotalUnread { get; set; }

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return;

            CurrentUserId = currentUser.Id;

            // Load Store and DeliveryPerson alongside each user so we
            // can resolve the correct display name per role without
            // extra round trips per user.
            var allUsers = await _userManager.Users
                .Include(u => u.Store)
                .Include(u => u.DeliveryPerson)
                .Where(x => x.Id != CurrentUserId)
                .ToListAsync();

            // SEARCH — by role-based display name:
            //   Store owner  -> Store.StoreName
            //   Delivery     -> DeliveryPerson.FullName
            //   Customer     -> User.FullName
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string term = SearchText.Trim().ToLower();

                SearchUsers = allUsers
                    .Select(u => new UserSearchResult
                    {
                        Id = u.Id,
                        FullName = GetDisplayName(u)
                    })
                    .Where(u => (u.FullName ?? "").ToLower().Contains(term))
                    .ToList();
            }

            // INBOX — same role-based display name logic
            var messages =
                await _messagingManager.GetMessagesForUserAsync(CurrentUserId);

            Inbox = messages
        .GroupBy(m =>
            m.SenderID == CurrentUserId
                ? m.ReceiverID
                : m.SenderID)
        .Select(g =>
        {
            var otherUser = allUsers.FirstOrDefault(x => x.Id == g.Key);

            return new InboxItem
            {
                UserId = g.Key,
                UserName = otherUser != null
                    ? GetDisplayName(otherUser)
                    : "User",
                LastMessage = g
                    .OrderByDescending(x => x.SentAt)
                    .FirstOrDefault()
                    ?.MessageText ?? "?? Shared Product",
                LastTime = g.Max(x => x.SentAt),
                UnreadCount = g.Count(x => x.ReceiverID == CurrentUserId && !x.IsRead)
            };
        })
        .OrderByDescending(x => x.LastTime)
        .ToList();

            TotalUnread = Inbox.Sum(x => x.UnreadCount);

            await _sessionManager.TouchAsync(CurrentUserId);
        }

        public async Task<bool> IsOnline(int userId)
        {
            return await _sessionManager.IsUserOnlineAsync(userId);
        }

        // =====================================================
        // LIVE SEARCH SUGGESTIONS — powers the dropdown under the
        // chat search box. Same role-based display name logic as
        // the full-page SearchText search above.
        // =====================================================
        public async Task<IActionResult> OnGetSearchUsersAsync(string term)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null || string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            {
                return new JsonResult(new { success = true, users = Array.Empty<object>() });
            }

            var normalizedTerm = term.Trim().ToLower();

            var candidates = await _userManager.Users
                .Include(u => u.Store)
                .Include(u => u.DeliveryPerson)
                .Where(u => u.Id != currentUser.Id)
                .ToListAsync();

            var matches = candidates
                .Select(u => new
                {
                    userId = u.Id,
                    userName = GetDisplayName(u)
                })
                .Where(u => (u.userName ?? "").ToLower().Contains(normalizedTerm))
                .OrderBy(u => u.userName)
                .Take(8)
                .ToList();

            return new JsonResult(new { success = true, users = matches });
        }

        // =====================================================
        // Resolves the correct display name for a user depending on
        // their role: Store owners are shown by their store name,
        // delivery people by their delivery profile's full name, and
        // everyone else (customers) by their account full name.
        // =====================================================
        private static string GetDisplayName(User user)
        {
            if (user.Store != null && !string.IsNullOrWhiteSpace(user.Store.StoreName))
            {
                return user.Store.StoreName;
            }

            if (user.DeliveryPerson != null && !string.IsNullOrWhiteSpace(user.DeliveryPerson.FullName))
            {
                return user.DeliveryPerson.FullName;
            }

            return !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : "User";
        }
    }

    public class InboxItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastTime { get; set; }
        public int UnreadCount { get; set; }
    }

    // Lightweight replacement for the old List<User> SearchUsers, so
    // the Razor view can keep using "@user.Id" and "@user.FullName"
    // unchanged while FullName now holds the resolved display name.
    public class UserSearchResult
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}