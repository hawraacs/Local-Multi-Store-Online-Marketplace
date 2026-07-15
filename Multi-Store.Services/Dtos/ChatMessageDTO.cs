// Entities/ChatMessage.cs
using Multi_Store.Core.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Services.Dtos
{
    public class ChatMessageDTO
    {
        [Key]
        public int MessageID { get; set; }

        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public int? OrderID { get; set; }
        public int? ProductID { get; set; }

        // NEW - additive, mirrors the entity. Set only for story-reply messages.
        public int? StoryID { get; set; }

        public string MessageText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual User Sender { get; set; } = null!;
        public virtual User Receiver { get; set; } = null!;
        public virtual Order? Order { get; set; }
        public ProductDTO? Product { get; set; }
    }
}
