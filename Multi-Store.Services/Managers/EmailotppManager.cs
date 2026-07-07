using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
   public class EmailotppManager
    {
        private readonly IConfiguration _config;

        public EmailotppManager(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendOtpAsync(string email, string otp)
{
    var host = _config["EmailSettings:SmtpServer"];
    var port = int.Parse(_config["EmailSettings:SmtpPort"]);
    var senderEmail = _config["EmailSettings:SenderEmail"];
    var password = _config["EmailSettings:Password"];
    var senderName = _config["EmailSettings:SenderName"];

    var message = new MailMessage
    {
        From = new MailAddress(senderEmail, senderName),
        Subject = "Your OTP Code",
        Body = $"Your OTP code is: {otp}\nIt expires in 5 minutes.",
        IsBodyHtml = false
    };

    message.To.Add(email);

    using var client = new SmtpClient(host, port)
    {
        Credentials = new NetworkCredential(senderEmail, password),
        EnableSsl = true
    };

    await client.SendMailAsync(message);
}
    }
}
