using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class StoreManager
    {
        private readonly IStoreRepository _storeRepository;
        private const string DefaultStoreOwnerPassword =
    "StoreOwner@12345";
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
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.OwnerUserID <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid customer account.");
            }

            if (string.IsNullOrWhiteSpace(dto.StoreName))
            {
                throw new InvalidOperationException(
                    "Store name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new InvalidOperationException(
                    "Store email is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                throw new InvalidOperationException(
                    "Store phone number is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.AddressLine1))
            {
                throw new InvalidOperationException(
                    "Store address is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.City))
            {
                throw new InvalidOperationException(
                    "City is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Area))
            {
                throw new InvalidOperationException(
                    "Area is required.");
            }

            // Search using the permanent original-Customer link.
            // The repository also supports old Stores where
            // RequestedByUserID is still null.
            var existingStore =
                await _storeRepository
                    .GetByRequestedByUserIdAsync(dto.OwnerUserID);

            if (existingStore != null)
            {
                if (string.Equals(
                        existingStore.Status?.Trim(),
                        "Pending",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "You already have a pending store request.");
                }

                if (string.Equals(
                        existingStore.Status?.Trim(),
                        "Approved",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "You are already a store owner.");
                }

                if (string.Equals(
                        existingStore.Status?.Trim(),
                        "Inactive",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        existingStore.Status?.Trim(),
                        "Suspended",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "A store account already exists but is currently inactive.");
                }

                if (string.Equals(
                        existingStore.Status?.Trim(),
                        "Rejected",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Permanently preserve the Customer who submitted
                    // the original Store request.
                    existingStore.RequestedByUserID ??=
                        dto.OwnerUserID;

                    // Before approval, OwnerUserID temporarily points
                    // to the original Customer account.
                    existingStore.OwnerUserID =
                        dto.OwnerUserID;

                    existingStore.StoreName =
                        dto.StoreName.Trim();

                    existingStore.Email =
                        dto.Email.Trim();

                    existingStore.PhoneNumber =
                        dto.PhoneNumber.Trim();

                    existingStore.AddressLine1 =
                        dto.AddressLine1.Trim();

                    existingStore.AddressLine2 =
                        string.IsNullOrWhiteSpace(dto.AddressLine2)
                            ? null
                            : dto.AddressLine2.Trim();

                    existingStore.City =
                        dto.City.Trim();

                    existingStore.Area =
                        dto.Area.Trim();

                    existingStore.Description =
                        dto.Description?.Trim()
                        ?? string.Empty;

                    existingStore.BusinessLicenseNumber =
                        string.IsNullOrWhiteSpace(
                            dto.BusinessLicenseNumber)
                            ? null
                            : dto.BusinessLicenseNumber.Trim();

                    existingStore.BusinessLicenseURL =
                        string.IsNullOrWhiteSpace(
                            dto.BusinessLicenseURL)
                            ? null
                            : dto.BusinessLicenseURL.Trim();

                    existingStore.Latitude =
                        dto.Latitude;

                    existingStore.Longitude =
                        dto.Longitude;

                    existingStore.HasFixedDeliveryFee =
                        dto.HasFixedDeliveryFee;

                    existingStore.FixedDeliveryFee =
                        dto.HasFixedDeliveryFee
                            ? dto.FixedDeliveryFee
                            : null;

                    existingStore.CommissionRate =
                        dto.CommissionRate;

                    existingStore.CODSupported =
                        dto.CODSupported;

                    existingStore.CODMaxLimit =
                        dto.CODMaxLimit;

                    existingStore.Status =
                        "Pending";

                    existingStore.ApprovedAt =
                        null;

                    existingStore.ApprovedBy =
                        null;

                    await _storeRepository
                        .UpdateAsync(existingStore);

                    return existingStore.StoreID;
                }

                throw new InvalidOperationException(
                    "A store request already exists for this customer.");
            }

            var store = new Store
            {
                // Before approval, both fields point to the Customer.
                // After approval, only OwnerUserID will be changed
                // to the generated StoreOwner account.
                OwnerUserID =
                    dto.OwnerUserID,

                RequestedByUserID =
                    dto.OwnerUserID,

                StoreName =
                    dto.StoreName.Trim(),

                Email =
                    dto.Email.Trim(),

                PhoneNumber =
                    dto.PhoneNumber.Trim(),

                AddressLine1 =
                    dto.AddressLine1.Trim(),

                AddressLine2 =
                    string.IsNullOrWhiteSpace(dto.AddressLine2)
                        ? null
                        : dto.AddressLine2.Trim(),

                City =
                    dto.City.Trim(),

                Area =
                    dto.Area.Trim(),

                Description =
                    dto.Description?.Trim()
                    ?? string.Empty,

                BusinessLicenseNumber =
                    string.IsNullOrWhiteSpace(
                        dto.BusinessLicenseNumber)
                        ? null
                        : dto.BusinessLicenseNumber.Trim(),

                BusinessLicenseURL =
                    string.IsNullOrWhiteSpace(
                        dto.BusinessLicenseURL)
                        ? null
                        : dto.BusinessLicenseURL.Trim(),

                Latitude =
                    dto.Latitude,

                Longitude =
                    dto.Longitude,

                HasFixedDeliveryFee =
                    dto.HasFixedDeliveryFee,

                FixedDeliveryFee =
                    dto.HasFixedDeliveryFee
                        ? dto.FixedDeliveryFee
                        : null,

                StoreCode =
                    "ST-" +
                    Guid.NewGuid()
                        .ToString("N")[..8]
                        .ToUpperInvariant(),

                Status =
                    "Pending",

                CreatedAt =
                    DateTime.UtcNow,

                CommissionRate =
                    dto.CommissionRate,

                CODSupported =
                    dto.CODSupported,

                CODMaxLimit =
                    dto.CODMaxLimit
            };

            var saved =
                await _storeRepository
                    .AddAsync(store);

            return saved.StoreID;
        }

        // =====================
        // APPROVAL
        // =====================

        public async Task<(string email, string password)>
     ApproveStoreWithAccountAsync(
         int storeId,
         int adminId,
         UserManager<User> userManager)
        {
            if (storeId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid store request.");
            }

            if (adminId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid admin account.");
            }

            if (userManager == null)
            {
                throw new ArgumentNullException(
                    nameof(userManager));
            }

            var store = await _storeRepository
                .GetByIdAsync(storeId);

            if (store == null)
            {
                throw new InvalidOperationException(
                    "Store request was not found.");
            }

            var cleanStatus = store.Status?.Trim();

            // Prevent creating a second StoreOwner account
            // if Approve is clicked again.
            if (string.Equals(
                    cleanStatus,
                    "Approved",
                    StringComparison.OrdinalIgnoreCase))
            {
                var existingStoreOwner =
                    await userManager.FindByIdAsync(
                        store.OwnerUserID.ToString());

                if (existingStoreOwner == null)
                {
                    throw new InvalidOperationException(
                        "The approved Store Owner account was not found.");
                }

                var hasStoreOwnerRole =
                    await userManager.IsInRoleAsync(
                        existingStoreOwner,
                        "StoreOwner");

                if (!hasStoreOwnerRole)
                {
                    var addRoleResult =
                        await userManager.AddToRoleAsync(
                            existingStoreOwner,
                            "StoreOwner");

                    if (!addRoleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            GetIdentityErrors(addRoleResult));
                    }
                }

                return (
                    existingStoreOwner.Email
                        ?? existingStoreOwner.UserName
                        ?? string.Empty,
                    "Use existing password"
                );
            }

            if (!string.Equals(
                    cleanStatus,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Only pending store requests can be approved.");
            }

            // Preserve the permanent link to the Customer
            // who originally submitted the request.
            store.RequestedByUserID ??=
                store.OwnerUserID;

            var originalCustomer =
                await userManager.FindByIdAsync(
                    store.RequestedByUserID.Value.ToString());

            if (originalCustomer == null)
            {
                throw new InvalidOperationException(
                    "The Customer account linked to this store request was not found.");
            }

            string storeOwnerEmail;
            var counter = 1;

            do
            {
                storeOwnerEmail =
                    $"storeowner{counter}@gmail.com";

                counter++;
            }
            while (await userManager.FindByEmailAsync(
                       storeOwnerEmail) != null);

            var storeOwnerUser = new User
            {
                UserName = storeOwnerEmail,
                Email = storeOwnerEmail,

                FullName =
                    !string.IsNullOrWhiteSpace(
                        originalCustomer.FullName)
                        ? originalCustomer.FullName.Trim()
                        : store.StoreName.Trim(),

                PhoneNumber =
                    store.PhoneNumber?.Trim(),

                EmailConfirmed = true,

                PhoneNumberConfirmed =
                    !string.IsNullOrWhiteSpace(
                        store.PhoneNumber),

                IsActive = true,

                MustChangePassword = false,

                CreatedAt = DateTime.UtcNow
            };

            var createResult =
                await userManager.CreateAsync(
                    storeOwnerUser,
                    DefaultStoreOwnerPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    GetIdentityErrors(createResult));
            }

            var roleResult =
                await userManager.AddToRoleAsync(
                    storeOwnerUser,
                    "StoreOwner");

            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(
                    storeOwnerUser);

                throw new InvalidOperationException(
                    GetIdentityErrors(roleResult));
            }

            try
            {
                // OwnerUserID now points to the generated
                // StoreOwner account.
                store.OwnerUserID =
                    storeOwnerUser.Id;

                // RequestedByUserID remains linked to
                // the original Customer account.
                store.Status =
                    "Approved";

                store.ApprovedAt =
                    DateTime.UtcNow;

                store.ApprovedBy =
                    adminId;

                // First month free.
                store.TrialStartDate =
                    DateTime.UtcNow;

                store.SubscriptionExpiryDate =
                    DateTime.UtcNow.AddMonths(1);

                store.SubscriptionStatus =
                    "Active";

                store.IsSuspended =
                    false;

                await _storeRepository
                    .UpdateAsync(store);
            }
            catch
            {
                // Do not leave an unused StoreOwner account
                // if updating the Store fails.
                await userManager.DeleteAsync(
                    storeOwnerUser);

                throw;
            }

            return (
                storeOwnerEmail,
                DefaultStoreOwnerPassword
            );
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
        private static string GetIdentityErrors(
    IdentityResult result)
        {
            return string.Join(
                " | ",
                result.Errors.Select(
                    error => error.Description));
        }

        private static bool StatusEquals(
            string? value,
            string expected)
        {
            return string.Equals(
                value?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}