using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories.Base;

public class StoreRepository : Repository<Store>, IStoreRepository
{
    private readonly ApplicationDbContext _context;

    public StoreRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    // =====================
    // STORE QUERIES
    // =====================

    public async Task<Store?> GetByCodeAsync(string storeCode)
        => await _context.Stores
            .FirstOrDefaultAsync(s => s.StoreCode == storeCode);

    public async Task<Store?> GetByOwnerIdAsync(int ownerUserId)
        => await _context.Stores
            .FirstOrDefaultAsync(s => s.OwnerUserID == ownerUserId);

    public async Task<Store?> GetStoreDetailsAsync(int storeId)
        => await _context.Stores
            .Include(s => s.Owner)
            .Include(s => s.Products)
            .Include(s => s.DeliveryAreas)
            .Include(s => s.Coupons)
            .Include(s => s.Reviews)
            .Include(s => s.Complaints)
            .FirstOrDefaultAsync(s => s.StoreID == storeId);

    public async Task<List<Store>> SearchStoresAsync(string keyword)
        => await _context.Stores
            .Where(s =>
                s.StoreName.Contains(keyword) ||
                s.Description.Contains(keyword) ||
                s.City.Contains(keyword) ||
                s.Area.Contains(keyword))
            .ToListAsync();

    // =====================
    // FILTERS
    // =====================

    public async Task<List<Store>> GetByStatusAsync(string status)
        => await _context.Stores
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<List<Store>> GetApprovedStoresAsync()
        => await _context.Stores
            .Where(s => s.Status == "Approved")
            .ToListAsync();

    public async Task<List<Store>> GetTopRatedStoresAsync(int count)
        => await _context.Stores
            .OrderByDescending(s => s.Rating)
            .Take(count)
            .ToListAsync();

    // =====================
    // FEED / PRODUCTS
    // =====================

    public async Task<List<Product>> GetFeedProductsAsync(int customerId)
    => await _context.Products
        .Include(p => p.Store)
        .Include(p => p.Images)

        .Include(p => p.Reviews)
            .ThenInclude(r => r.Customer)
                .ThenInclude(c => c.User)

        .Where(p => _context.StoreFollows
            .Any(f =>
                f.CustomerID == customerId &&
                f.StoreID == p.StoreID))

        .OrderByDescending(p => p.ProductID)
        .ToListAsync();

    public async Task<List<Product>> GetStoreProductsAsync(int storeId)
        => await _context.Products
            .Where(p => p.StoreID == storeId && p.IsActive)
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    // =====================
    // FOLLOW SYSTEM
    // =====================

    public async Task<int> GetFollowersCountAsync(int storeId)
        => await _context.StoreFollows
            .CountAsync(f => f.StoreID == storeId);

    public async Task<bool> IsFollowingAsync(int customerId, int storeId)
        => await _context.StoreFollows
            .AnyAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

    public async Task FollowStoreAsync(int customerId, int storeId)
    {
        var exists = await _context.StoreFollows
            .AnyAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

        if (exists) return;

        _context.StoreFollows.Add(new StoreFollow
        {
            CustomerID = customerId,
            StoreID = storeId,
            FollowedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task UnfollowStoreAsync(int customerId, int storeId)
    {
        var follow = await _context.StoreFollows
            .FirstOrDefaultAsync(x => x.CustomerID == customerId && x.StoreID == storeId);

        if (follow == null) return;

        _context.StoreFollows.Remove(follow);
        await _context.SaveChangesAsync();
    }

    // =====================
    // REVIEWS
    // =====================

    public async Task<List<Review>> GetStoreReviewsAsync(int storeId)
        => await _context.Reviews
            .Include(r => r.Customer)
                .ThenInclude(c => c.User)
            .Where(r => r.StoreID == storeId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
}