using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    using global::Multi_Store.Core.Entities;
    using global::Multi_Store.Core.Interfaces;
    using global::Multi_Store.Infrastructure.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Multi_Store.Core.Entities;
    using Multi_Store.Core.Interfaces;
    using Multi_Store.Infrastructure.Data;
    using System.Security.Claims;
    using System.Threading.Tasks;

namespace Multi_Store.Services
{
    public class CurrentStoreService : ICurrentStoreService
        {
            private readonly IHttpContextAccessor _httpContextAccessor;
            private readonly ApplicationDbContext _context;

            public CurrentStoreService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
            {
                _httpContextAccessor = httpContextAccessor;
                _context = context;
            }

            private int? GetCurrentUserId()
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(userIdClaim, out int userId))
                    return userId;

                return null;
            }

            public async Task<Store?> GetCurrentStoreAsync()
            {
                var userId = GetCurrentUserId();
                if (userId == null) return null;

                return await _context.Stores
                    .FirstOrDefaultAsync(s => s.OwnerUserID == userId.Value && s.Status == "Active");
            }

            public async Task<int?> GetCurrentStoreIdAsync()
            {
                var store = await GetCurrentStoreAsync();
                return store?.StoreID;
            }

            public async Task<bool> IsStoreOwnerAsync()
            {
                var userId = GetCurrentUserId();
                if (userId == null) return false;

                return await _context.Stores.AnyAsync(s => s.OwnerUserID == userId.Value);
            }

            public async Task<bool> ValidateStoreAccessAsync(int storeId)
            {
                var userId = GetCurrentUserId();
                if (userId == null) return false;

                return await _context.Stores.AnyAsync(s => s.StoreID == storeId && s.OwnerUserID == userId.Value);
            }
        }
    }