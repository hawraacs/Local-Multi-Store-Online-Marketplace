// Entities/ChatMessage.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Core.Entities
{
    public class ChatMessage
    {
        [Key]
        public int MessageID { get; set; }

        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public int? OrderID { get; set; }
        public int? ProductID { get; set; }
        public string MessageText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual User Sender { get; set; } = null!;
        public virtual User Receiver { get; set; } = null!;
        public virtual Order? Order { get; set; }
        public virtual Product? Product { get; set; }
    }
}