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
        private readonly IChatMessageRepository _chatMessageRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IMapper _mapper;

        public MessagingManager(
            IChatMessageRepository chatMessageRepository,
            IAuditLogRepository auditLogRepository,
            IMapper mapper)
        {
            _chatMessageRepository = chatMessageRepository;
            _auditLogRepository = auditLogRepository;
            _mapper = mapper;
        }

        // =========================================================
        // 1. GET METHODS
        // =========================================================

        /// <summary>
        /// Get all messages
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> GetAllMessagesAsync()
        {
            var messages = await _chatMessageRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ChatMessageDTO>>(messages);
        }

        /// <summary>
        /// Get message by ID
        /// </summary>
        public async Task<ChatMessageDTO?> GetMessageByIdAsync(int messageId)
        {
            var message = await _chatMessageRepository.GetByIdAsync(messageId);
            return _mapper.Map<ChatMessageDTO?>(message);
        }

        /// <summary>
        /// Get conversation between two users
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> GetConversationAsync(
            int userId1,
            int userId2)
        {
            var messages = await _chatMessageRepository
                .GetConversationAsync(userId1, userId2);

            return _mapper.Map<IEnumerable<ChatMessageDTO>>(messages);
        }

        /// <summary>
        /// Get unread messages for user
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> GetUnreadMessagesAsync(int userId)
        {
            var messages = await _chatMessageRepository
                .GetUnreadMessagesAsync(userId);

            return _mapper.Map<IEnumerable<ChatMessageDTO>>(messages);
        }

        /// <summary>
        /// Get messages related to order
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> GetMessagesByOrderAsync(int orderId)
        {
            var messages = await _chatMessageRepository
                .GetMessagesByOrderAsync(orderId);

            return _mapper.Map<IEnumerable<ChatMessageDTO>>(messages);
        }

        /// <summary>
        /// Get messages related to product
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> GetMessagesByProductAsync(int productId)
        {
            var messages = await _chatMessageRepository
                .GetMessagesByProductAsync(productId);

            return _mapper.Map<IEnumerable<ChatMessageDTO>>(messages);
        }

        // =========================================================
        // 2. SEND / CREATE MESSAGE
        // =========================================================

        /// <summary>
        /// Send message
        /// </summary>
        public async Task<int> SendMessageAsync(
            ChatMessageDTO messageDTO,
            string ipAddress,
            string userAgent)
        {
            // Validation
            if (messageDTO.SenderID <= 0)
                throw new Exception("Invalid sender.");

            if (messageDTO.ReceiverID <= 0)
                throw new Exception("Invalid receiver.");

            if (messageDTO.SenderID == messageDTO.ReceiverID)
                throw new Exception("Sender and receiver cannot be the same.");

            if (string.IsNullOrWhiteSpace(messageDTO.MessageText)
                && string.IsNullOrWhiteSpace(messageDTO.ImageUrl))
            {
                throw new Exception("Message text or image is required.");
            }

            var message = _mapper.Map<ChatMessage>(messageDTO);

            message.IsRead = false;
            message.SentAt = DateTime.UtcNow;

            await _chatMessageRepository.AddAsync(message);

            // Audit log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = message.SenderID,
                Action = "SendMessage",
                EntityName = "ChatMessage",
                EntityID = message.MessageID.ToString(),
                OldValue = null,
                NewValue = $"Message sent to UserID: {message.ReceiverID}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return message.MessageID;
        }

        // =========================================================
        // 3. UPDATE MESSAGE
        // =========================================================

        /// <summary>
        /// Update message
        /// </summary>
        public async Task UpdateMessageAsync(
            ChatMessageDTO messageDTO,
            string ipAddress,
            string userAgent)
        {
            var existingMessage =
                await _chatMessageRepository.GetByIdAsync(messageDTO.MessageID);

            if (existingMessage == null)
                throw new Exception("Message not found.");

            var oldValue = existingMessage.MessageText;

            existingMessage.MessageText = messageDTO.MessageText;
            existingMessage.ImageUrl = messageDTO.ImageUrl;

            await _chatMessageRepository.UpdateAsync(existingMessage);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = existingMessage.SenderID,
                Action = "UpdateMessage",
                EntityName = "ChatMessage",
                EntityID = existingMessage.MessageID.ToString(),
                OldValue = oldValue,
                NewValue = existingMessage.MessageText,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // 4. DELETE MESSAGE
        // =========================================================

        /// <summary>
        /// Delete message
        /// </summary>
        public async Task DeleteMessageAsync(
            int messageId,
            string ipAddress,
            string userAgent)
        {
            var message = await _chatMessageRepository.GetByIdAsync(messageId);

            if (message == null)
                throw new Exception("Message not found.");

            await _chatMessageRepository.DeleteAsync(message);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = message.SenderID,
                Action = "DeleteMessage",
                EntityName = "ChatMessage",
                EntityID = message.MessageID.ToString(),
                OldValue = message.MessageText,
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // 5. READ MANAGEMENT
        // =========================================================

        /// <summary>
        /// Mark conversation messages as read
        /// </summary>
        public async Task MarkMessagesAsReadAsync(
            int senderId,
            int receiverId,
            string ipAddress,
            string userAgent)
        {
            await _chatMessageRepository
                .MarkAsReadAsync(senderId, receiverId);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = receiverId,
                Action = "MarkMessagesAsRead",
                EntityName = "ChatMessage",
                EntityID = null,
                OldValue = "Unread",
                NewValue = $"Messages from UserID {senderId} marked as read",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // 6. CHECK METHODS
        // =========================================================

        /// <summary>
        /// Check if message exists
        /// </summary>
        public async Task<bool> MessageExistsAsync(int messageId)
        {
            var message = await _chatMessageRepository.GetByIdAsync(messageId);
            return message != null;
        }

        /// <summary>
        /// Check if message is read
        /// </summary>
        public async Task<bool> IsMessageReadAsync(int messageId)
        {
            var message = await _chatMessageRepository.GetByIdAsync(messageId);

            if (message == null)
                return false;

            return message.IsRead;
        }

        // =========================================================
        // 7. COUNT METHODS
        // =========================================================

        /// <summary>
        /// Get unread message count
        /// </summary>
        public async Task<int> GetUnreadMessageCountAsync(int userId)
        {
            var unreadMessages =
                await _chatMessageRepository.GetUnreadMessagesAsync(userId);

            return unreadMessages.Count;
        }

        /// <summary>
        /// Get total messages count
        /// </summary>
        public async Task<int> GetTotalMessagesCountAsync()
        {
            var messages = await _chatMessageRepository.GetAllAsync();
            return messages.Count();
        }

        /// <summary>
        /// Get conversation messages count
        /// </summary>
        public async Task<int> GetConversationMessagesCountAsync(
            int userId1,
            int userId2)
        {
            var messages = await _chatMessageRepository
                .GetConversationAsync(userId1, userId2);

            return messages.Count;
        }

        // =========================================================
        // 8. SEARCH METHODS
        // =========================================================

        /// <summary>
        /// Search messages in conversation
        /// </summary>
        public async Task<IEnumerable<ChatMessageDTO>> SearchMessagesAsync(
            int userId1,
            int userId2,
            string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetConversationAsync(userId1, userId2);
            }

            var messages = await _chatMessageRepository
                .GetConversationAsync(userId1, userId2);

            var filteredMessages = messages
                .Where(m => !string.IsNullOrWhiteSpace(m.MessageText) &&
                            m.MessageText.Contains(
                                searchTerm,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<ChatMessageDTO>>(filteredMessages);
        }

        // =========================================================
        // 9. DASHBOARD / SUMMARY
        // =========================================================

        /// <summary>
        /// Get messaging dashboard for user
        /// </summary>
        public async Task<object> GetMessagingDashboardAsync(int userId)
        {
            var unreadMessages =
                await _chatMessageRepository.GetUnreadMessagesAsync(userId);

            var allMessages =
                await _chatMessageRepository.GetAllAsync();

            var sentMessages = allMessages
                .Where(m => m.SenderID == userId)
                .Count();

            var receivedMessages = allMessages
                .Where(m => m.ReceiverID == userId)
                .Count();

            return new
            {
                TotalSentMessages = sentMessages,
                TotalReceivedMessages = receivedMessages,
                TotalUnreadMessages = unreadMessages.Count,
                LastUnreadMessageDate = unreadMessages
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault()?.SentAt
            };
        }
    }
}