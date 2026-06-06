using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Multi_Store.Services.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public SmtpEmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Recipient email is required.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.SmtpServer) ||
                _emailSettings.SmtpPort <= 0 ||
                string.IsNullOrWhiteSpace(_emailSettings.SenderEmail) ||
                string.IsNullOrWhiteSpace(_emailSettings.Username) ||
                string.IsNullOrWhiteSpace(_emailSettings.Password))
            {
                throw new InvalidOperationException("Email settings are missing. Check appsettings.json.");
            }

            var cleanPassword = _emailSettings.Password.Replace(" ", "").Trim();

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailSettings.SenderEmail,
                    _emailSettings.SenderName),

                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            message.To.Add(email);

            using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(
                    _emailSettings.Username,
                    cleanPassword)
            };

            await client.SendMailAsync(message);
        }
    }
}