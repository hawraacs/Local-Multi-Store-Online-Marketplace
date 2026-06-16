using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
using Multi_Store.Services.Managers;
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

        public ChatConversationModel(
            UserManager<User> userManager,
            MessagingManager messagingManager,
            SessionManager sessionManager,
            ProductManager productManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _messagingManager = messagingManager;
            _sessionManager = sessionManager;
            _productManager = productManager;
            _environment = environment;
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

            return RedirectToPage(new { userId = receiverId });
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