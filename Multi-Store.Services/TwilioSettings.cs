using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services
{
    public class TwilioSettings
    {
        public string AccountSid { get; set; }
            = string.Empty;

        public string AuthToken { get; set; }
            = string.Empty;

        public string VerifyServiceSid { get; set; }
            = string.Empty;
    }
}
