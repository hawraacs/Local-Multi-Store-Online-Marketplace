using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class StoreManager
    {
        private readonly IStoreRepository _storeRepository;

        public StoreManager(IStoreRepository storeRepository)
        {
            _storeRepository = storeRepository;
        }

        // =====================
        // BASIC
        // =====================

        public Task<Store?> GetStoreByIdAsync(int id)
            => _storeRepository.GetByIdAsync(id);

        public Task<Store?> GetByUserIdAsync(int userId)
            => _storeRepository.GetByOwnerIdAsync(userId);

        // =====================
        // REGISTER STORE
        // =====================

        public async Task<int> RegisterStoreAsync(StoreDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var existingStore = await _storeRepository.GetByOwnerIdAsync(dto.OwnerUserID);

            if (existingStore != null)
            {
                if (existingStore.Status == "Pending")
                    throw new InvalidOperationException("You already have a pending store request.");

                if (existingStore.Status == "Approved")
                    throw new InvalidOperationException("You are already a store owner.");

                if (existingStore.Status == "Rejected")
                {
                    existingStore.StoreName = dto.StoreName;
                    existingStore.Email = dto.Email;
                    existingStore.PhoneNumber = dto.PhoneNumber;
                    existingStore.AddressLine1 = dto.AddressLine1;
                    existingStore.AddressLine2 = dto.AddressLine2;
                    existingStore.City = dto.City;
                    existingStore.Area = dto.Area;
                    existingStore.Description = dto.Description;
                    existingStore.BusinessLicenseNumber = dto.BusinessLicenseNumber;
                    existingStore.BusinessLicenseURL = dto.BusinessLicenseURL;
                    existingStore.Latitude = dto.Latitude;
                    existingStore.Longitude = dto.Longitude;
                    existingStore.HasFixedDeliveryFee = dto.HasFixedDeliveryFee;
                    existingStore.FixedDeliveryFee = dto.FixedDeliveryFee;
                    existingStore.Status = "Pending";
                    existingStore.ApprovedAt = null;
                    existingStore.ApprovedBy = null;

                    await _storeRepository.UpdateAsync(existingStore);
                    return existingStore.StoreID;
                }
            }

            var store = new Store
            {
                OwnerUserID = dto.OwnerUserID,
                StoreName = dto.StoreName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                AddressLine1 = dto.AddressLine1,
                AddressLine2 = dto.AddressLine2,
                City = dto.City,
                Area = dto.Area,
                Description = dto.Description,
                BusinessLicenseNumber = dto.BusinessLicenseNumber,
                BusinessLicenseURL = dto.BusinessLicenseURL,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                HasFixedDeliveryFee = dto.HasFixedDeliveryFee,
                FixedDeliveryFee = dto.FixedDeliveryFee,
                StoreCode = "ST-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                CommissionRate = dto.CommissionRate,
                CODSupported = dto.CODSupported,
                CODMaxLimit = dto.CODMaxLimit
            };

            var saved = await _storeRepository.AddAsync(store);
            return saved.StoreID;
        }

        // =====================
        // APPROVAL
        // =====================

        public async Task<(string email, string password)> ApproveStoreWithAccountAsync(
            int storeId,
            int adminId,
            UserManager<User> userManager)
        {
            var store = await _storeRepository.GetByIdAsync(storeId)
                ?? throw new Exception("Store not found");

            var owner = await userManager.FindByIdAsync(store.OwnerUserID.ToString())
                ?? throw new Exception("User not found");

            if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
            {
                var result = await userManager.AddToRoleAsync(owner, "StoreOwner");

                if (!result.Succeeded)
                    throw new Exception(string.Join(",", result.Errors.Select(e => e.Description)));
            }

            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            // Subscription
            store.TrialStartDate = DateTime.UtcNow;
            store.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(30);
            store.SubscriptionStatus = "Active";

            // First month free
            store.TrialStartDate = DateTime.UtcNow;
            store.SubscriptionExpiryDate = DateTime.UtcNow.AddMonths(1);
            store.SubscriptionStatus = "Active";

            await _storeRepository.UpdateAsync(store);

            return (owner.Email ?? string.Empty, "Use existing password");
        }

        public async Task RejectStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId)
                ?? throw new Exception("Store not found");

            store.Status = "Rejected";

            await _storeRepository.UpdateAsync(store);
        }

        public async Task ActivateStoreAsync(int storeId, int adminId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId)
                ?? throw new Exception("Store not found");

            store.Status = "Approved";
            store.ApprovedAt ??= DateTime.UtcNow;
            store.ApprovedBy ??= adminId;

            if (store.SubscriptionExpiryDate == null)
            {
                store.TrialStartDate = DateTime.UtcNow;
                store.SubscriptionExpiryDate = DateTime.UtcNow.AddMonths(1);
                store.SubscriptionStatus = "Active";
            }

            await _storeRepository.UpdateAsync(store);
        }
        public async Task<bool> IsStoreApprovedAsync(int userId)
        {
            var store = await _storeRepository.GetByOwnerIdAsync(userId);
            return store != null && store.Status == "Approved";
        }

        public async Task DeactivateStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId)
                ?? throw new Exception("Store not found");

            store.Status = "Inactive";

            await _storeRepository.UpdateAsync(store);
        }

        // =====================
        // STORE DATA
        // =====================

        public async Task<IEnumerable<StoreDTO>> GetAllStoresAsync()
        {
            var stores = await _storeRepository.GetAllAsync();

            return stores.Select(s => new StoreDTO
            {
                StoreID = s.StoreID,
                OwnerUserID = s.OwnerUserID,
                StoreName = s.StoreName,
                StoreCode = s.StoreCode,
                Description = s.Description,
                LogoURL = s.LogoURL,
                PhoneNumber = s.PhoneNumber,
                Email = s.Email,
                AddressLine1 = s.AddressLine1,
                AddressLine2 = s.AddressLine2,
                City = s.City,
                Area = s.Area,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                HasFixedDeliveryFee = s.HasFixedDeliveryFee,
                FixedDeliveryFee = s.FixedDeliveryFee,
                BusinessLicenseNumber = s.BusinessLicenseNumber,
                BusinessLicenseURL = s.BusinessLicenseURL,
                Rating = s.Rating,
                TotalRatings = s.TotalRatings,
                Status = s.Status,
                CommissionRate = s.CommissionRate,
                CODSupported = s.CODSupported,
                CODMaxLimit = s.CODMaxLimit,
                CreatedAt = s.CreatedAt,
                ApprovedAt = s.ApprovedAt,
                ApprovedBy = s.ApprovedBy ,

                SubscriptionStatus = s.SubscriptionStatus,
                SubscriptionExpiryDate = s.SubscriptionExpiryDate,
                OwnerEmail = s.Owner != null ? s.Owner.Email : null
            });
        }

        public Task<List<Store>> GetNearbyStoresAsync(double lat, double lng, double radiusKm = 10)
            => _storeRepository.GetApprovedStoresAsync(); // keep repo logic there if needed

        // =====================
        // FEED / FOLLOW
        // =====================

        public Task<List<Product>> GetFeedProductsAsync(int customerId)
            => _storeRepository.GetFeedProductsAsync(customerId);

        public Task<int> GetFollowersCountAsync(int storeId)
            => _storeRepository.GetFollowersCountAsync(storeId);

        public Task<List<Product>> GetStoreProductsAsync(int storeId)
            => _storeRepository.GetStoreProductsAsync(storeId);

        public Task FollowStoreAsync(int customerId, int storeId)
            => _storeRepository.FollowStoreAsync(customerId, storeId);

        public Task UnfollowStoreAsync(int customerId, int storeId)
            => _storeRepository.UnfollowStoreAsync(customerId, storeId);

        public Task<bool> IsFollowingAsync(int customerId, int storeId)
            => _storeRepository.IsFollowingAsync(customerId, storeId);

        public Task<List<Review>> GetStoreReviewsAsync(int storeId)
            => _storeRepository.GetStoreReviewsAsync(storeId);
        public Task DeleteProductReviewAsync(
     int reviewId,
     int storeOwnerId)
        {
            return _storeRepository.DeleteProductReviewAsync(
                reviewId,
                storeOwnerId);
        }
    }
}