using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Multi_Store.Services.Managers
{
    public class BoostPricingOption
    {
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
        public string Label => $"{DurationDays} days";
    }

    public class BoostManager
    {
        private readonly ApplicationDbContext _context;

        // Simple fixed tiers — adjust freely.
        public static readonly List<BoostPricingOption> PricingOptions = new()
        {
            new BoostPricingOption { DurationDays = 3,  Price = 9.00m  },
            new BoostPricingOption { DurationDays = 7,  Price = 18.00m },
            new BoostPricingOption { DurationDays = 14, Price = 30.00m },
            new BoostPricingOption { DurationDays = 30, Price = 55.00m },
        };

        public BoostManager(ApplicationDbContext context)
        {
            _context = context;
        }

        public static BoostPricingOption? GetOption(int durationDays) =>
            PricingOptions.FirstOrDefault(o => o.DurationDays == durationDays);

        // ============ CREATE REQUEST ============
        public async Task<ProductBoost> CreateBoostRequestAsync(int storeId, int productId, int durationDays)
        {
            var option = GetOption(durationDays)
                ?? throw new ArgumentException("Invalid boost duration selected.");

            var boost = new ProductBoost
            {
                StoreID = storeId,
                ProductID = productId,
                DurationDays = durationDays,
                AmountPaid = option.Price,
                Status = "PendingPayment",
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductBoosts.Add(boost);
            await _context.SaveChangesAsync();
            return boost;
        }

        // ============ ACTIVATE AFTER SUCCESSFUL PAYMENT ============
        public async Task ActivateBoostAsync(int boostId)
        {
            var boost = await _context.ProductBoosts.FirstOrDefaultAsync(b => b.ProductBoostID == boostId);
            if (boost == null) return;

            var now = DateTime.UtcNow;
            boost.Status = "Active";
            boost.StartDate = now;
            boost.EndDate = now.AddDays(boost.DurationDays);

            await _context.SaveChangesAsync();
        }

        // ============ EXPIRE OLD BOOSTS (call this at the top of feed/explore reads) ============
        public async Task ExpireDueBoostsAsync()
        {
            var now = DateTime.UtcNow;
            var due = await _context.ProductBoosts
                .Where(b => b.Status == "Active" && b.EndDate != null && b.EndDate <= now)
                .ToListAsync();

            if (due.Count == 0) return;

            foreach (var b in due) b.Status = "Expired";
            await _context.SaveChangesAsync();
        }

        // ============ QUERY HELPERS ============
        public async Task<ProductBoost?> GetActiveBoostForProductAsync(int productId)
        {
            var now = DateTime.UtcNow;
            return await _context.ProductBoosts
                .Where(b => b.ProductID == productId && b.Status == "Active" && b.EndDate > now)
                .OrderByDescending(b => b.EndDate)
                .FirstOrDefaultAsync();
        }

        public async Task<HashSet<int>> GetActiveBoostedProductIdsAsync()
        {
            var now = DateTime.UtcNow;
            var ids = await _context.ProductBoosts
                .Where(b => b.Status == "Active" && b.EndDate > now)
                .Select(b => b.ProductID)
                .ToListAsync();

            return ids.ToHashSet();
        }

        public async Task<ProductBoost?> GetCurrentBoostForOwnerAsync(int storeId, int productId)
        {
            return await _context.ProductBoosts
                .Where(b => b.StoreID == storeId && b.ProductID == productId &&
                            (b.Status == "Active" || b.Status == "PendingPayment"))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();
        }

        // ============ ADMIN ============
        public async Task<List<ProductBoost>> GetAllBoostsAsync(string? statusFilter = null)
        {
            var query = _context.ProductBoosts
                .Include(b => b.Store)
                .Include(b => b.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(b => b.Status == statusFilter);

            return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        }

        public async Task<decimal> GetTotalBoostRevenueAsync()
        {
            return await _context.ProductBoosts
                .Where(b => b.Status == "Active" || b.Status == "Expired")
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0m;
        }
    }
}