// ==========================================================
// ADMIN ENTITY
// File: Core/Entities/Admin.cs
// ==========================================================

using System;

namespace Multi_Store.Core.Entities
{
    public class Admin
    {
        // Primary Key
        public int AdminID { get; set; }

        // Foreign Key
        public int UserID { get; set; }

        // Attributes
        public string Role { get; set; } = string.Empty;

        public string Permissions { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relationships
        public virtual User User { get; set; } = null!;
    }
}