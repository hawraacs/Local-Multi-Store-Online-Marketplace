

using Multi_Store.Core.Entities;
using System;

namespace Multi_Store.Services.Dtos
{
    public class AdminDTO
    {
        // Primary Key
        public int AdminID { get; set; }

        // Foreign Key
        public int UserID { get; set; }

        // Attributes
        public string Role { get; set; } = string.Empty;

        public string Permissions { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // Relationships
        public virtual User User { get; set; } = null!;
    }
}