using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class ChatConversationModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly MessagingManager _messagingManager;

        public ChatConversationModel(UserManager<User> userManager, MessagingManager messagingManager)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
        }

        public List<ChatMessageDTO> Messages { get; set; } = new();

        public User OtherUser { get; set; }

        public int CurrentUserId { get; set; }
        public int ReceiverId { get; set; }

        [BindProperty]
        public string MessageText { get; set; }

        public async Task<IActionResult> OnGetAsync(int userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            CurrentUserId = user.Id;
            ReceiverId = userId;

            OtherUser = _userManager.Users.FirstOrDefault(u => u.Id == userId);

            Messages = (await _messagingManager.GetConversationAsync(CurrentUserId, userId))
                .OrderBy(m => m.SentAt)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostSendAsync(int receiverId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            if (!string.IsNullOrWhiteSpace(MessageText))
            {
                await _messagingManager.SendMessageAsync(
                    new ChatMessageDTO
                    {
                        SenderID = user.Id,
                        ReceiverID = receiverId,
                        MessageText = MessageText
                    },
                    "",
                    ""
                );
            }

            return RedirectToPage(new { userId = receiverId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int messageId, int userId)
        {
            await _messagingManager.DeleteMessageAsync(messageId, "", "");
            return RedirectToPage(new { userId });
        }
    }
}