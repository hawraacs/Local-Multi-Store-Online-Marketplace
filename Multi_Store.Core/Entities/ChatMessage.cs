using System;

namespace Multi_Store.Core.Entities
{
    public class ChatMessage
    {
        // Primary Key
        public int MessageID { get; set; }

        // Foreign Keys
        public int SenderID { get; set; }

        public int ReceiverID { get; set; }

        public int? OrderID { get; set; }

        public int? ProductID { get; set; }

        // Attributes
        public string MessageText { get; set; } = string.Empty;

        public string? ImageURL { get; set; }

        public bool IsRead { get; set; }

        public DateTime SentAt { get; set; }

        // Relationships

        // Many Messages belong to one Sender
        public User? Sender { get; set; }

        // Many Messages belong to one Receiver
        public User? Receiver { get; set; }

        // Many Messages can reference one Order
        public Order? Order { get; set; }

        // Many Messages can reference one Product
        public Product? Product { get; set; }
    }
}