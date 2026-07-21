using Microsoft.Extensions.Options;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Settings;
using Twilio;
using Twilio.Rest.Verify.V2.Service;
using Twilio.Types;

namespace Multi_Store.Infrastructure.Services
{
    public class TwilioService : ITwilioService
    {
        private readonly TwilioSettings _settings;

        public TwilioService(IOptions<TwilioSettings> options)
        {
            _settings = options.Value;

            TwilioClient.Init(
                _settings.AccountSid,
                _settings.AuthToken);
        }

        public async Task SendOtpAsync(string phoneNumber)
        {
            await VerificationResource.CreateAsync(
                to: phoneNumber,
                channel: "sms",
                pathServiceSid: _settings.VerifyServiceSid);
        }

        public async Task<bool> VerifyOtpAsync(string phoneNumber, string code)
        {
            var result = await VerificationCheckResource.CreateAsync(
                to: phoneNumber,
                code: code,
                pathServiceSid: _settings.VerifyServiceSid);

            return result.Status == "approved";
        }
    }
}