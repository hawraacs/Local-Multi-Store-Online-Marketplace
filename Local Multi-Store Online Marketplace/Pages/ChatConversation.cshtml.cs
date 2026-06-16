using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local_Multi_Store_Online_Marketplace.Pages
{
    public class ChatConversationModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly MessagingManager _messagingManager;
        private readonly SessionManager _sessionManager;
        private readonly ProductManager _productManager;

        public ChatConversationModel(
            UserManager<User> userManager,
            MessagingManager messagingManager,
            SessionManager sessionManager,
            ProductManager productManager)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
            _sessionManager = sessionManager;
            _productManager = productManager;
        }

        public List<ChatMessageDTO> Messages { get; set; } = new();
        public User OtherUser { get; set; }

        public int CurrentUserId { get; set; }
        public int ReceiverId { get; set; }

        public bool IsOnlineUser { get; set; }

        [BindProperty]
        public string MessageText { get; set; }

        public async Task<IActionResult> OnGetAsync(int userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            CurrentUserId = user.Id;
            ReceiverId = userId;

            OtherUser = await _userManager.Users
    .FirstOrDefaultAsync(x => x.Id == userId);

            Messages =
     (await _messagingManager
     .GetConversationAsync(CurrentUserId, userId))
     .OrderBy(x => x.SentAt)
     .ToList();

            

            IsOnlineUser = await _sessionManager.IsUserOnlineAsync(userId);

            await _sessionManager.TouchAsync(CurrentUserId);

            return Page();
        }

        public async Task<IActionResult> OnPostSendAsync(int receiverId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            if (!string.IsNullOrWhiteSpace(MessageText))
            {
                await _messagingManager.SendMessageAsync(new ChatMessageDTO
                {
                    SenderID = user.Id,
                    ReceiverID = receiverId,
                    MessageText = MessageText
                }, "", "");
            }

            return RedirectToPage(new { userId = receiverId });
        }

        public async Task<IActionResult> OnPostDeleteMessageAsync(int messageId, int userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            await _messagingManager.DeleteMessageAsync(messageId, "", "");
            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostDeleteChatAsync(int userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            await _messagingManager.DeleteConversationAsync(user.Id, userId, "", "");
            return RedirectToPage("/Chat");
        }

        public async Task<IActionResult> OnPostShareProductAsync(int productId, int receiverId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            await _messagingManager.SendProductAsync(
                user.Id,
                receiverId,
                productId);

            return RedirectToPage(new { userId = receiverId });
        }
    }
}