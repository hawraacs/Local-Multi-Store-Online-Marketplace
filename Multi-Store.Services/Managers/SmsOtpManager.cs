using Microsoft.Extensions.Options;
using Multi_Store.Services;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Verify.V2.Service;
using Multi_Store.Infrastructure.Settings;

namespace Multi_Store.Services.Managers
{
    public class SmsOtpManager
    {
        private readonly TwilioSettings _settings;

        public SmsOtpManager(
            IOptions<TwilioSettings> options)
        {
            _settings =
                options.Value;
        }

        public async Task SendOtpAsync(
            string phoneNumber)
        {
            var normalizedPhone =
                NormalizePhone(phoneNumber);

            ValidateSettings();

            if (string.IsNullOrWhiteSpace(
                    normalizedPhone))
            {
                throw new InvalidOperationException(
                    "A valid international phone number is required.");
            }

            TwilioClient.Init(
                _settings.AccountSid.Trim(),
                _settings.AuthToken.Trim());

            var verification =
                await VerificationResource.CreateAsync(
                    to:
                        normalizedPhone,

                    channel:
                        "sms",

                    pathServiceSid:
                        _settings.VerifyServiceSid.Trim());

            if (!string.Equals(
                    verification.Status,
                    "pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Twilio could not start phone verification.");
            }
        }

        public async Task<bool> VerifyOtpAsync(
            string phoneNumber,
            string otpCode)
        {
            var normalizedPhone =
                NormalizePhone(phoneNumber);

            ValidateSettings();

            if (string.IsNullOrWhiteSpace(
                    normalizedPhone))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    otpCode))
            {
                return false;
            }

            TwilioClient.Init(
                _settings.AccountSid.Trim(),
                _settings.AuthToken.Trim());

            var verificationCheck =
                await VerificationCheckResource.CreateAsync(
                    to:
                        normalizedPhone,

                    code:
                        otpCode.Trim(),

                    pathServiceSid:
                        _settings.VerifyServiceSid.Trim());

            return string.Equals(
                verificationCheck.Status,
                "approved",
                StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateSettings()
        {
            var accountSid =
                _settings.AccountSid?.Trim()
                ?? string.Empty;

            var authToken =
                _settings.AuthToken?.Trim()
                ?? string.Empty;

            var verifyServiceSid =
                _settings.VerifyServiceSid?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(
                    accountSid)
                ||
                string.IsNullOrWhiteSpace(
                    authToken)
                ||
                string.IsNullOrWhiteSpace(
                    verifyServiceSid))
            {
                throw new InvalidOperationException(
                    "Twilio Verify settings are missing.");
            }

            if (!accountSid.StartsWith(
                    "AC",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Twilio AccountSid is invalid. It must start with AC.");
            }

            if (!verifyServiceSid.StartsWith(
                    "VA",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Twilio VerifyServiceSid is invalid. It must start with VA.");
            }
        }

        private static string NormalizePhone(
            string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(
                    phoneNumber))
            {
                return string.Empty;
            }

            return Regex.Replace(
                phoneNumber.Trim(),
                @"[\s()-]",
                string.Empty);
        }
    }
}