using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class MessagingManager
    {
        private readonly IChatMessageRepository _chatRepo;
        private readonly IAuditLogRepository _audit;
        private readonly IMapper _mapper;

        public MessagingManager(
            IChatMessageRepository chatRepo,
            IAuditLogRepository audit,
            IMapper mapper)
        {
            _chatRepo = chatRepo;
            _audit = audit;
            _mapper = mapper;
        }

        public async Task<List<ChatMessageDTO>> GetMessagesForUserAsync(int userId)
        {
            if (userId <= 0)
            {
                return new List<ChatMessageDTO>();
            }

            var messages = await _chatRepo.GetMessagesByUserAsync(userId);

            return _mapper.Map<List<ChatMessageDTO>>(messages);
        }

        public async Task<List<ChatMessageDTO>> GetConversationAsync(int user1Id, int user2Id)
        {
            if (user1Id <= 0 || user2Id <= 0)
            {
                return new List<ChatMessageDTO>();
            }

            var messages = await _chatRepo.GetConversationAsync(user1Id, user2Id);

            return _mapper.Map<List<ChatMessageDTO>>(messages);
        }

        public async Task MarkConversationAsReadAsync(int senderId, int receiverId)
        {
            if (senderId <= 0 || receiverId <= 0)
            {
                return;
            }

            await _chatRepo.MarkAsReadAsync(senderId, receiverId);
        }

        public async Task<int> SendMessageAsync(
            ChatMessageDTO dto,
            string ip,
            string agent)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.SenderID <= 0)
            {
                throw new ArgumentException("SenderID is required.", nameof(dto.SenderID));
            }

            if (dto.ReceiverID <= 0)
            {
                throw new ArgumentException("ReceiverID is required.", nameof(dto.ReceiverID));
            }

            var message = new ChatMessage
            {
                SenderID = dto.SenderID,
                ReceiverID = dto.ReceiverID,
                MessageText = string.IsNullOrWhiteSpace(dto.MessageText)
                    ? string.Empty
                    : dto.MessageText.Trim(),
                ProductID = dto.ProductID,
                ImageUrl = dto.ImageUrl,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            await _chatRepo.AddAsync(message);

            await _audit.AddAsync(new AuditLog
            {
                UserID = message.SenderID,
                Action = "SendMessage",
                EntityName = "ChatMessage",
                EntityID = message.MessageID.ToString(),
                NewValue = "Message sent",
                ActionDate = DateTime.UtcNow
            });

            return message.MessageID;
        }

        public async Task<int> SendProductAsync(
            int senderId,
            int receiverId,
            int productId)
        {
            if (senderId <= 0)
            {
                throw new ArgumentException("Sender ID is required.", nameof(senderId));
            }

            if (receiverId <= 0)
            {
                throw new ArgumentException("Receiver ID is required.", nameof(receiverId));
            }

            if (productId <= 0)
            {
                throw new ArgumentException("Product ID is required.", nameof(productId));
            }

            var dto = new ChatMessageDTO
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                ProductID = productId,
                MessageText = $"Product shared with you. Product ID: {productId}"
            };

            return await SendMessageAsync(dto, string.Empty, string.Empty);
        }

        public async Task DeleteMessageAsync(
            int messageId,
            string ip,
            string agent)
        {
            if (messageId <= 0)
            {
                return;
            }

            var message = await _chatRepo.GetByIdAsync(messageId);

            if (message == null)
            {
                return;
            }

            await _chatRepo.DeleteAsync(message);

            await _audit.AddAsync(new AuditLog
            {
                UserID = message.SenderID,
                Action = "DeleteMessage",
                EntityName = "ChatMessage",
                EntityID = message.MessageID.ToString(),
                OldValue = "Message deleted",
                ActionDate = DateTime.UtcNow
            });
        }

        public async Task DeleteConversationAsync(
            int user1Id,
            int user2Id,
            string ip,
            string agent)
        {
            if (user1Id <= 0 || user2Id <= 0)
            {
                return;
            }

            var messages = await _chatRepo.GetConversationAsync(user1Id, user2Id);

            if (messages == null || !messages.Any())
            {
                return;
            }

            foreach (var message in messages)
            {
                await _chatRepo.DeleteAsync(message);
            }

            await _audit.AddAsync(new AuditLog
            {
                UserID = user1Id,
                Action = "DeleteConversation",
                EntityName = "ChatMessage",
                EntityID = $"{user1Id}-{user2Id}",
                OldValue = "Conversation deleted",
                ActionDate = DateTime.UtcNow
            });
        }
    }
}