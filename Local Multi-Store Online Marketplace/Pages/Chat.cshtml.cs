using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class ChatModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly MessagingManager _messagingManager;

        public ChatModel(UserManager<User> userManager, MessagingManager messagingManager)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
        }

        public List<User> SearchUsers { get; set; } = new();
        public List<InboxItem> Inbox { get; set; } = new();

        public int CurrentUserId { get; set; }
        public string SearchText { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            CurrentUserId = user.Id;

            SearchText = Request.Query["searchText"].ToString()?.Trim();

            var allUsers = _userManager.Users.ToList();

            // =========================
            // ?? INBOX (always load)
            // =========================
            var messages = await _messagingManager.GetMessagesForUserAsync(CurrentUserId);

            Inbox = messages
                .GroupBy(m => m.SenderID == CurrentUserId ? m.ReceiverID : m.SenderID)
                .Select(g => new InboxItem
                {
                    UserId = g.Key,
                    UserName = allUsers.FirstOrDefault(u => u.Id == g.Key)?.FullName
                               ?? allUsers.FirstOrDefault(u => u.Id == g.Key)?.UserName
                               ?? "Unknown",
                    LastMessage = g.OrderByDescending(x => x.SentAt).FirstOrDefault()?.MessageText ?? "",
                    LastTime = g.Max(x => x.SentAt)
                })
                .OrderByDescending(x => x.LastTime)
                .ToList();

            // =========================
            // ?? SEARCH (ROBUST FIX)
            // =========================
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string key = SearchText.ToLower();

                SearchUsers = allUsers
                    .Where(u => u.Id != CurrentUserId)
                    .Where(u =>
                        (!string.IsNullOrEmpty(u.FullName) && u.FullName.ToLower().Contains(key)) ||
                        (!string.IsNullOrEmpty(u.UserName) && u.UserName.ToLower().Contains(key)) ||
                        (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(key))
                    )
                    .ToList();
            }
        }
    }

    public class InboxItem
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastTime { get; set; }
    }
}