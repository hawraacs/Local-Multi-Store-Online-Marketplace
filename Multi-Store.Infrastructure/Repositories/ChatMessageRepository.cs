using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Infrastructure.Repositories
{
    public class ChatMessageRepository : Repository<ChatMessage>, IChatMessageRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatMessageRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ChatMessage>> GetConversationAsync(int userId1, int userId2)
        {
            return await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.Product)
                .Where(m =>
                    (m.SenderID == userId1 && m.ReceiverID == userId2) ||
                    (m.SenderID == userId2 && m.ReceiverID == userId1))
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ChatMessage>> GetUnreadMessagesAsync(int userId)
        {
            return await _context.ChatMessages
                .Where(m => m.ReceiverID == userId && !m.IsRead)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int senderId, int receiverId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.SenderID == senderId &&
                            m.ReceiverID == receiverId &&
                            !m.IsRead)
                .ToListAsync();

            foreach (var msg in messages)
            {
                msg.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ChatMessage>> GetMessagesByOrderAsync(int orderId)
        {
            return await _context.ChatMessages
                .Where(m => m.OrderID == orderId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ChatMessage>> GetMessagesByProductAsync(int productId)
        {
            return await _context.ChatMessages
                .Where(m => m.ProductID == productId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }
        public async Task<List<ChatMessage>> GetMessagesByUserAsync(int userId)
        {
            return await _context.ChatMessages
                .Where(m => m.SenderID == userId || m.ReceiverID == userId)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
        }
        public async Task DeleteRangeAsync(IEnumerable<ChatMessage> messages)
        {
            _context.ChatMessages.RemoveRange(messages);
            await _context.SaveChangesAsync();
        }
    }
}
