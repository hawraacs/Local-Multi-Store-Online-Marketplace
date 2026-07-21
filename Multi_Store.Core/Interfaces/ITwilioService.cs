using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Interfaces
{
        public interface ITwilioService
        {
            Task SendOtpAsync(string phoneNumber);

            Task<bool> VerifyOtpAsync(string phoneNumber, string code);
        }
    }