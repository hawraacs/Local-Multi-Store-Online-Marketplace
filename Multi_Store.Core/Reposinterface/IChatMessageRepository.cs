using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Reposinterface
{
    public interface IChatMessageRepository : IRepository<ChatMessage>
    {
        // Get conversation between two users
        Task<IReadOnlyList<ChatMessage>> GetConversationAsync(int userId1, int userId2);

        // Get unread messages for a user
        Task<IReadOnlyList<ChatMessage>> GetUnreadMessagesAsync(int userId);

        // Mark messages as read
        Task MarkAsReadAsync(int senderId, int receiverId);

        // Get messages related to an order
        Task<IReadOnlyList<ChatMessage>> GetMessagesByOrderAsync(int orderId);

        // Get messages related to a product
        Task<IReadOnlyList<ChatMessage>> GetMessagesByProductAsync(int productId);

        // Get messages that are replies to a specific Story (NEW - additive, same pattern
        // as GetMessagesByOrderAsync/GetMessagesByProductAsync above)
        Task<IReadOnlyList<ChatMessage>> GetMessagesByStoryAsync(int storyId);

        Task<List<ChatMessage>> GetMessagesByUserAsync(int userId);
        Task DeleteRangeAsync(IEnumerable<ChatMessage> messages);
    }
}
