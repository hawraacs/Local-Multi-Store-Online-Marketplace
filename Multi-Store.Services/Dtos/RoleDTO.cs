using Multi_Store.Core.Entities;
using System.Collections.Generic;

namespace Multi_Store.Services.Dtos
{
    public class RoleDTO
    {
        public int RoleID { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        // Navigation Properties
        public ICollection<User>? Users { get; set; }
    }
}