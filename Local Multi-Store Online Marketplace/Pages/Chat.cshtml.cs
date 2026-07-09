using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class ChatModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly MessagingManager _messagingManager;
        private readonly SessionManager _sessionManager;

        public ChatModel(
            UserManager<User> userManager,
            MessagingManager messagingManager,
            SessionManager sessionManager)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
            _sessionManager = sessionManager;
        }

        public int CurrentUserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchText { get; set; } = "";

        public List<User> SearchUsers { get; set; } = new();
        public List<InboxItem> Inbox { get; set; } = new();
        public int TotalUnread { get; set; }

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return;

            CurrentUserId = currentUser.Id;

            var allUsers = _userManager.Users
                .Where(x => x.Id != CurrentUserId)
                .ToList();

            // SEARCH
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string term = SearchText.Trim().ToLower();

                SearchUsers = allUsers
                    .Where(u =>
                        (u.UserName ?? "").ToLower().Contains(term)
                        || (u.Email ?? "").ToLower().Contains(term))
                    .ToList();
            }

            // INBOX
            var messages =
                await _messagingManager.GetMessagesForUserAsync(CurrentUserId);

            Inbox = messages
        .GroupBy(m =>
            m.SenderID == CurrentUserId
                ? m.ReceiverID
                : m.SenderID)
        .Select(g => new InboxItem
        {
            UserId = g.Key,
            UserName = allUsers
                .FirstOrDefault(x => x.Id == g.Key)
                ?.UserName ?? "User",
            LastMessage = g
                .OrderByDescending(x => x.SentAt)
                .FirstOrDefault()
                ?.MessageText ?? "?? Shared Product",
            LastTime = g.Max(x => x.SentAt),
            UnreadCount = g.Count(x => x.ReceiverID == CurrentUserId && !x.IsRead)
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
    }

    public class InboxItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastTime { get; set; }
        public int UnreadCount { get; set; }
    }
}