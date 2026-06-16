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
            var msgs = await _chatRepo.GetMessagesByUserAsync(userId);
            return _mapper.Map<List<ChatMessageDTO>>(msgs);
        }

        public async Task<List<ChatMessageDTO>> GetConversationAsync(int u1, int u2)
        {
            var msgs = await _chatRepo.GetConversationAsync(u1, u2);
            return _mapper.Map<List<ChatMessageDTO>>(msgs);
        }

        public async Task<int> SendMessageAsync(ChatMessageDTO dto, string ip, string agent)
        {
            var msg = new ChatMessage
            {
                SenderID = dto.SenderID,
                ReceiverID = dto.ReceiverID,
                MessageText = dto.MessageText,
                ProductID = dto.ProductID,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            await _chatRepo.AddAsync(msg);

            await _audit.AddAsync(new AuditLog
            {
                UserID = msg.SenderID,
                Action = "SendMessage",
                EntityName = "ChatMessage",
                EntityID = msg.MessageID.ToString(),
                NewValue = "Message sent",
                ActionDate = DateTime.UtcNow
            });

            return msg.MessageID;
        }

        public async Task DeleteMessageAsync(int messageId, string ip, string agent)
        {
            var msg = await _chatRepo.GetByIdAsync(messageId);
            if (msg == null) return;

            await _chatRepo.DeleteAsync(msg);
        }

        public async Task DeleteConversationAsync(int u1, int u2, string ip, string agent)
        {
            var msgs = await _chatRepo.GetConversationAsync(u1, u2);

            await _chatRepo.DeleteRangeAsync(msgs);
        }

        public async Task<int> SendProductAsync(int senderId, int receiverId, int productId)
        {
            var msg = new ChatMessage
            {
                SenderID = senderId,
                ReceiverID = receiverId,
                ProductID = productId,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            await _chatRepo.AddAsync(msg);
            return msg.MessageID;
        }
    }
}