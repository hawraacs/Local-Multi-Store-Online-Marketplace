using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Dtos
{
    public class OtpDto
    {
        public int UserId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
        // RegistrationEmail, RegistrationPhone, LoginEmail, LoginPhone

        public string Destination { get; set; } = string.Empty;
        // email or phone (for sending)

        public int ExpiryMinutes { get; set; } = 5;
    }
}
