using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
using Local_Multi_Store_Online_Marketplace.Hubs;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationManager _notificationManager;
        private readonly Multi_Store.Core.Reposinterface.IChatMessageRepository _chatMessageRepository;
        private readonly IHubContext<AppHub> _hub; // NEW — pushes chat messages live via SignalR

        public ChatConversationModel(
            UserManager<User> userManager,
            MessagingManager messagingManager,
            SessionManager sessionManager,
            ProductManager productManager,
            IWebHostEnvironment environment,
            NotificationManager notificationManager,
            Multi_Store.Core.Reposinterface.IChatMessageRepository chatMessageRepository,
            IHubContext<AppHub> hub) // NEW
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
            _sessionManager = sessionManager;
            _productManager = productManager;
            _environment = environment;
            _notificationManager = notificationManager;
            _chatMessageRepository = chatMessageRepository;
            _hub = hub; // NEW
        }

        public List<ChatMessageDTO> Messages { get; set; } = new();

        public User? OtherUser { get; set; }

        public int CurrentUserId { get; set; }

        public int ReceiverId { get; set; }

        public bool IsOnlineUser { get; set; }

        [BindProperty]
        public string? MessageText { get; set; }

        [BindProperty]
        public IFormFile? AttachmentFile { get; set; }

        public async Task<IActionResult> OnGetAsync(int userId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            if (userId <= 0 || userId == user.Id)
            {
                return RedirectToPage("/Chat");
            }

            CurrentUserId = user.Id;
            ReceiverId = userId;

            OtherUser = await _userManager.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (OtherUser == null)
            {
                return RedirectToPage("/Chat");
            }

            await _messagingManager.MarkConversationAsReadAsync(userId, CurrentUserId);

            var unreadMessageNotifications = await _notificationManager.GetUnreadAsync(CurrentUserId);

            foreach (var n in unreadMessageNotifications.Where(x => x.Type == "Message" && x.ReferenceID.HasValue))
            {
                var relatedMessage = await _chatMessageRepository.GetByIdAsync(n.ReferenceID!.Value);

                if (relatedMessage != null && relatedMessage.SenderID == userId)
                {
                    await _notificationManager.MarkAsReadAsync(n.NotificationID);
                }
            }

            Messages = (await _messagingManager.GetConversationAsync(CurrentUserId, userId))
                .OrderBy(x => x.SentAt)
                .ToList();

            IsOnlineUser = await _sessionManager.IsUserOnlineAsync(userId);

            await _sessionManager.TouchAsync(CurrentUserId);

            return Page();
        }

        public async Task<IActionResult> OnPostSendAsync(int receiverId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            if (receiverId <= 0 || receiverId == user.Id)
            {
                return RedirectToPage("/Chat");
            }

            var receiverExists = await _userManager.Users
                .AnyAsync(u => u.Id == receiverId);

            if (!receiverExists)
            {
                return RedirectToPage("/Chat");
            }

            string? fileUrl = null;
            string finalMessageText = MessageText?.Trim() ?? string.Empty;

            if (AttachmentFile != null && AttachmentFile.Length > 0)
            {
                var savedFile = await SaveChatFileAsync(AttachmentFile);

                fileUrl = savedFile.Url;

                if (savedFile.IsVoice)
                {
                    finalMessageText = "[VOICE]";
                }
                else if (savedFile.IsImage)
                {
                    if (string.IsNullOrWhiteSpace(finalMessageText))
                    {
                        finalMessageText = "[IMAGE]";
                    }
                }
                else
                {
                    finalMessageText = $"[FILE] {savedFile.OriginalFileName}";
                }
            }

            if (!string.IsNullOrWhiteSpace(finalMessageText) ||
                !string.IsNullOrWhiteSpace(fileUrl))
            {
                await _messagingManager.SendMessageAsync(new ChatMessageDTO
                {
                    SenderID = user.Id,
                    ReceiverID = receiverId,
                    MessageText = finalMessageText,
                    ImageUrl = fileUrl
                }, "", "");

                // NEW — push the message live to both sides of the
                // conversation via the shared AppHub SignalR connection.
                // The receiver sees it appear instantly with no refresh;
                // the sender's own page also gets it in case they have
                // this same conversation open in another tab/device.
                await BroadcastChatMessageAsync(user, receiverId, finalMessageText, fileUrl);
            }

            await _sessionManager.TouchAsync(user.Id);

            return RedirectToPage(new { userId = receiverId });
        }

        public async Task<IActionResult> OnPostDeleteMessageAsync(int messageId, int userId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            await _messagingManager.DeleteMessageAsync(messageId, "", "");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostDeleteChatAsync(int userId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            await _messagingManager.DeleteConversationAsync(user.Id, userId, "", "");

            return RedirectToPage("/Chat");
        }

        public async Task<IActionResult> OnPostShareProductAsync(int productId, int receiverId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            await _messagingManager.SendProductAsync(
                user.Id,
                receiverId,
                productId);

            // NEW — notify both sides live that a product was shared.
            // The client-side listener reloads the conversation on
            // receiving isProduct=true, since rendering the full product
            // card needs image/price/description this lightweight
            // broadcast doesn't carry.
            try
            {
                var payload = new
                {
                    senderId = user.Id,
                    receiverId = receiverId,
                    isProduct = true
                };

                await _hub.Clients.User(receiverId.ToString()).SendAsync("ReceiveChatMessage", payload);
                await _hub.Clients.User(user.Id.ToString()).SendAsync("ReceiveChatMessage", payload);
            }
            catch
            {
                // Never let a broadcast failure break product sharing —
                // the message itself is already saved at this point.
            }

            return RedirectToPage(new { userId = receiverId });
        }

        /// <summary>
        /// Broadcasts a newly sent chat message to both the sender and
        /// receiver via AppHub. Wrapped in try/catch so a hub/connection
        /// failure never breaks sending the message itself — the message
        /// is already safely saved by the time this runs.
        /// </summary>
        private async Task BroadcastChatMessageAsync(User sender, int receiverId, string messageText, string? imageUrl)
        {
            try
            {
                var senderDisplayName = sender.FullName ?? sender.UserName ?? "User";

                var payload = new
                {
                    senderId = sender.Id,
                    receiverId = receiverId,
                    senderName = senderDisplayName,
                    messageText = messageText,
                    imageUrl = imageUrl,
                    sentAt = DateTime.UtcNow.ToString("o") // ISO 8601 — matches data-utc on the client
                };

                await _hub.Clients.User(receiverId.ToString()).SendAsync("ReceiveChatMessage", payload);
                await _hub.Clients.User(sender.Id.ToString()).SendAsync("ReceiveChatMessage", payload);
            }
            catch
            {
                // Swallow — live push is a nice-to-have, not critical path.
            }
        }

        private async Task<ChatSavedFile> SaveChatFileAsync(IFormFile file)
        {
            const long maxSize = 10 * 1024 * 1024;

            if (file.Length > maxSize)
            {
                throw new InvalidOperationException("File size cannot exceed 10 MB.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            var allowedExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt",
                ".mp3", ".wav", ".webm", ".ogg", ".m4a"
            };

            if (string.IsNullOrWhiteSpace(extension) ||
                !allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("File type is not allowed.");
            }

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsFolder = Path.Combine(webRootPath, "uploads", "chat");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadsFolder, uniqueName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var contentType = file.ContentType ?? string.Empty;

            var isImage =
                contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                extension == ".jpg" ||
                extension == ".jpeg" ||
                extension == ".png" ||
                extension == ".gif" ||
                extension == ".webp";

            var isVoice =
                contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                extension == ".mp3" ||
                extension == ".wav" ||
                extension == ".webm" ||
                extension == ".ogg" ||
                extension == ".m4a";

            return new ChatSavedFile
            {
                Url = $"/uploads/chat/{uniqueName}",
                OriginalFileName = Path.GetFileName(file.FileName),
                IsImage = isImage,
                IsVoice = isVoice
            };
        }
    }

    public class ChatSavedFile
    {
        public string Url { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public bool IsImage { get; set; }

        public bool IsVoice { get; set; }
    }
}