using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
   
        public class User
        {
            public int UserID { get; set; }

            public int RoleID { get; set; }

            public string FullName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string PhoneNumber { get; set; } = string.Empty;

            public string PasswordHash { get; set; } = string.Empty;

            public bool IsActive { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime? LastLoginAt { get; set; }

            // Navigation Properties
            public Role? Role { get; set; }

            public Customer? Customer { get; set; }

            public Store? Store { get; set; }
        }
   
}
