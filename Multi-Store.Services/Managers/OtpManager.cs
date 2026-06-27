using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class OtpManager
    {
        private readonly ApplicationDbContext _context;

        public OtpManager(ApplicationDbContext context)
        {
            _context = context;
        }

        // Generate secure OTP
        public string GenerateOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);

            int value = BitConverter.ToInt32(bytes, 0);
            value = Math.Abs(value % 900000) + 100000;

            return value.ToString();
        }

        // CREATE OTP using DTO
        public async Task<OtpDto> CreateOtpAsync(OtpDto dto)
        {
            var code = GenerateOtp();

            // remove old OTPs for same purpose
            var oldOtps = _context.OtpCodes
                .Where(x => x.UserId == dto.UserId && x.Type == dto.Type && !x.IsUsed);

            _context.OtpCodes.RemoveRange(oldOtps);

            var otp = new OtpCode
            {
                UserId = dto.UserId,
                Code = code,
                Type = dto.Type,
                CreatedAt = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddMinutes(dto.ExpiryMinutes),
                IsUsed = false,
                AttemptCount = 0
            };

            _context.OtpCodes.Add(otp);
            await _context.SaveChangesAsync();

            dto.Code = code; // return generated code to caller

            return dto;
        }

        // VERIFY OTP
        public async Task<bool> VerifyOtpAsync(int userId, string type, string code)
        {
            var otp = await _context.OtpCodes
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Type == type &&
                    !x.IsUsed);

            if (otp == null)
                return false;

            if (otp.ExpiryTime < DateTime.UtcNow)
                return false;

            if (otp.AttemptCount >= 5)
                return false;

            otp.AttemptCount++;

            if (otp.Code != code)
            {
                await _context.SaveChangesAsync();
                return false;
            }

            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}