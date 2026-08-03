using System;

namespace Multi_Store.Services.Dtos
{
    public class DeliveryReviewDTO
    {
        public int DeliveryReviewID { get; set; }

        public int CustomerID { get; set; }
        public string? CustomerName { get; set; }   // flat field, mirrors ReviewDTO.CustomerName

        public int DeliveryPersonID { get; set; }

        public int AssignmentID { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}