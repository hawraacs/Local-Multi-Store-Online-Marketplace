using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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

        public Task<Store?> GetByRequestedByUserIdAsync(
            int requestedByUserId)
            => _storeRepository
                .GetByRequestedByUserIdAsync(
                    requestedByUserId);

        // =====================
        // REGISTER STORE
        // =====================

        /*
         * Use this method from the Store Request page.
         * Expected business validation failures are returned as a result
         * instead of being thrown as exceptions.
         */
        public async Task<StoreRegistrationResult>
            TryRegisterStoreAsync(
                StoreDTO? dto)
        {
            if (dto == null)
            {
                return StoreRegistrationResult.Failure(
                    "Invalid store request.");
            }

            if (dto.OwnerUserID <= 0)
            {
                return StoreRegistrationResult.Failure(
                    "Invalid customer account.");
            }

            NormalizeStoreRequest(
                dto);

            var validationError =
                GetStoreRequestValidationError(
                    dto);

            if (!string.IsNullOrWhiteSpace(
                    validationError))
            {
                return StoreRegistrationResult.Failure(
                    validationError);
            }

            var existingStore =
                await _storeRepository
                    .GetByRequestedByUserIdAsync(
                        dto.OwnerUserID);

            if (existingStore != null)
            {
                if (StatusEquals(
                        existingStore.Status,
                        "Pending"))
                {
                    return StoreRegistrationResult.Failure(
                        "You already have a pending store request.");
                }

                if (StatusEquals(
                        existingStore.Status,
                        "Approved"))
                {
                    return StoreRegistrationResult.Failure(
                        "You are already a store owner.");
                }

                if (StatusEquals(
                        existingStore.Status,
                        "Inactive") ||
                    StatusEquals(
                        existingStore.Status,
                        "Suspended"))
                {
                    return StoreRegistrationResult.Failure(
                        "A store account already exists but is currently inactive.");
                }

                if (!StatusEquals(
                        existingStore.Status,
                        "Rejected"))
                {
                    return StoreRegistrationResult.Failure(
                        "A store request already exists for this customer.");
                }
            }

            var excludedStoreId =
                existingStore?.StoreID;

            var phoneUsed =
                await _storeRepository
                    .IsPhoneUsedAsync(
                        dto.PhoneNumber,
                        excludedStoreId);

            if (phoneUsed)
            {
                return StoreRegistrationResult.Failure(
                    "This phone number is already used by another store.");
            }

            var licenseUsed =
                await _storeRepository
                    .IsBusinessLicenseNumberUsedAsync(
                        dto.BusinessLicenseNumber!,
                        excludedStoreId);

            if (licenseUsed)
            {
                return StoreRegistrationResult.Failure(
                    "This business license number is already registered.");
            }

            if (existingStore != null)
            {
                ApplyStoreRequestData(
                    existingStore,
                    dto);

                existingStore.RequestedByUserID ??=
                    dto.OwnerUserID;

                existingStore.OwnerUserID =
                    dto.OwnerUserID;

                existingStore.Status =
                    "Pending";

                existingStore.CreatedAt =
                    DateTime.UtcNow;

                existingStore.ApprovedAt =
                    null;

                existingStore.ApprovedBy =
                    null;

                existingStore.SubscriptionStatus =
                    "Pending";

                existingStore.IsSuspended =
                    false;

                await _storeRepository
                    .UpdateAsync(
                        existingStore);

                return StoreRegistrationResult.Success(
                    existingStore.StoreID,
                    "Your updated store request was resubmitted successfully.");
            }

            var store =
                new Store
                {
                    OwnerUserID =
                        dto.OwnerUserID,

                    RequestedByUserID =
                        dto.OwnerUserID,

                    StoreCode =
                        "ST-" +
                        Guid.NewGuid()
                            .ToString("N")[..8]
                            .ToUpperInvariant(),

                    Status =
                        "Pending",

                    SubscriptionStatus =
                        "Pending",

                    CreatedAt =
                        DateTime.UtcNow
                };

            ApplyStoreRequestData(
                store,
                dto);

            var saved =
                await _storeRepository
                    .AddAsync(
                        store);

            return StoreRegistrationResult.Success(
                saved.StoreID,
                "Your store request was submitted successfully.");
        }

        /*
         * Compatibility method for any older code that still calls
         * RegisterStoreAsync. The Store Request page should use
         * TryRegisterStoreAsync instead.
         */
        public async Task<int> RegisterStoreAsync(
            StoreDTO dto)
        {
            var result =
                await TryRegisterStoreAsync(
                    dto);

            if (!result.Succeeded ||
                !result.StoreId.HasValue)
            {
                throw new InvalidOperationException(
                    result.Message);
            }

            return result.StoreId.Value;
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
                ApprovedBy = s.ApprovedBy,

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
        private static void NormalizeStoreRequest(
            StoreDTO dto)
        {
            dto.StoreName =
                dto.StoreName?.Trim()
                ?? string.Empty;

            dto.Email =
                dto.Email?.Trim()
                    .ToLowerInvariant()
                ?? string.Empty;

            dto.PhoneNumber =
                NormalizeLebanesePhone(
                    dto.PhoneNumber);

            dto.Description =
                dto.Description?.Trim()
                ?? string.Empty;

            dto.AddressLine1 =
                dto.AddressLine1?.Trim()
                ?? string.Empty;

            dto.AddressLine2 =
                string.IsNullOrWhiteSpace(
                    dto.AddressLine2)
                    ? null
                    : dto.AddressLine2.Trim();

            dto.City =
                dto.City?.Trim()
                ?? string.Empty;

            dto.Area =
                dto.Area?.Trim()
                ?? string.Empty;

            dto.BusinessLicenseNumber =
                dto.BusinessLicenseNumber?.Trim()
                ?? string.Empty;

            dto.BusinessLicenseURL =
                dto.BusinessLicenseURL?.Trim();
        }

        private static string?
            GetStoreRequestValidationError(
                StoreDTO dto)
        {
            if (dto.StoreName.Length < 2 ||
                dto.StoreName.Length > 100)
            {
                return
                    "Store name must contain between 2 and 100 characters.";
            }

            if (!new EmailAddressAttribute()
                    .IsValid(
                        dto.Email) ||
                dto.Email.Length > 150)
            {
                return
                    "Enter a valid business contact email.";
            }

            if (string.IsNullOrWhiteSpace(
                    dto.PhoneNumber))
            {
                return
                    "Enter a valid Lebanese phone number.";
            }

            if (dto.Description.Length < 20 ||
                dto.Description.Length > 1000)
            {
                return
                    "Store description must contain between 20 and 1000 characters.";
            }

            if (dto.AddressLine1.Length < 5 ||
                dto.AddressLine1.Length > 250)
            {
                return
                    "Store address must contain between 5 and 250 characters.";
            }

            if (string.IsNullOrWhiteSpace(
                    dto.BusinessLicenseNumber) ||
                dto.BusinessLicenseNumber.Length < 3 ||
                dto.BusinessLicenseNumber.Length > 50)
            {
                return
                    "Business license number must contain between 3 and 50 characters.";
            }

            if (string.IsNullOrWhiteSpace(
                    dto.BusinessLicenseURL))
            {
                return
                    "Business license document is required.";
            }

            if (!IsValidCityArea(
                    dto.City,
                    dto.Area))
            {
                return
                    "The selected area does not match the selected city.";
            }

            if (!IsInsideLebanon(
                    dto.Latitude,
                    dto.Longitude))
            {
                return
                    "The store location must be inside Lebanon.";
            }

            if (dto.HasFixedDeliveryFee &&
                (!dto.FixedDeliveryFee.HasValue ||
                 dto.FixedDeliveryFee.Value < 0))
            {
                return
                    "Enter a valid fixed delivery fee.";
            }

            return null;
        }

        private static void ApplyStoreRequestData(
            Store store,
            StoreDTO dto)
        {
            store.StoreName =
                dto.StoreName;

            store.Email =
                dto.Email;

            store.PhoneNumber =
                dto.PhoneNumber;

            store.Description =
                dto.Description;

            store.AddressLine1 =
                dto.AddressLine1;

            store.AddressLine2 =
                dto.AddressLine2;

            store.City =
                dto.City;

            store.Area =
                dto.Area;

            store.Latitude =
                dto.Latitude;

            store.Longitude =
                dto.Longitude;

            store.BusinessLicenseNumber =
                dto.BusinessLicenseNumber;

            store.BusinessLicenseURL =
                dto.BusinessLicenseURL;

            store.HasFixedDeliveryFee =
                dto.HasFixedDeliveryFee;

            store.FixedDeliveryFee =
                dto.HasFixedDeliveryFee
                    ? dto.FixedDeliveryFee
                    : null;

            store.CommissionRate =
                dto.CommissionRate;

            store.CODSupported =
                dto.CODSupported;

            store.CODMaxLimit =
                dto.CODMaxLimit;
        }

        private static string NormalizeLebanesePhone(
            string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(
                    phoneNumber))
            {
                return string.Empty;
            }

            var value =
                Regex.Replace(
                    phoneNumber.Trim(),
                    @"[\s()-]",
                    string.Empty);

            if (value.StartsWith(
                    "00961",
                    StringComparison.Ordinal))
            {
                value =
                    "+" + value.Substring(2);
            }
            else if (value.StartsWith(
                         "961",
                         StringComparison.Ordinal))
            {
                value =
                    "+" + value;
            }
            else if (value.StartsWith(
                         "0",
                         StringComparison.Ordinal))
            {
                value =
                    "+961" + value.Substring(1);
            }
            else if (!value.StartsWith(
                         "+",
                         StringComparison.Ordinal))
            {
                value =
                    "+961" + value;
            }

            return Regex.IsMatch(
                value,
                @"^\+961[0-9]{7,8}$")
                    ? value
                    : string.Empty;
        }

        private static bool IsValidCityArea(
            string city,
            string area)
        {
            var expectedArea =
                city switch
                {
                    "Beirut" =>
                        "Beirut",

                    "Tripoli" =>
                        "North Lebanon",

                    "Saida" or "Tyre" =>
                        "South Lebanon",

                    "Zahle" =>
                        "Bekaa",

                    "Jounieh" or "Byblos" or
                    "Aley" or "Baabda" =>
                        "Mount Lebanon",

                    "Nabatieh" =>
                        "Nabatieh",

                    _ =>
                        string.Empty
                };

            return !string.IsNullOrWhiteSpace(
                       expectedArea)
                   &&
                   string.Equals(
                       expectedArea,
                       area,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideLebanon(
            decimal latitude,
            decimal longitude)
        {
            return latitude >= 33.0m &&
                   latitude <= 34.8m &&
                   longitude >= 35.0m &&
                   longitude <= 36.8m;
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

    public sealed class StoreRegistrationResult
    {
        public bool Succeeded { get; private set; }

        public int? StoreId { get; private set; }

        public string Message { get; private set; }
            = string.Empty;

        public static StoreRegistrationResult Success(
            int storeId,
            string message)
        {
            return new StoreRegistrationResult
            {
                Succeeded = true,
                StoreId = storeId,
                Message = message
            };
        }

        public static StoreRegistrationResult Failure(
            string message)
        {
            return new StoreRegistrationResult
            {
                Succeeded = false,
                StoreId = null,
                Message = message
            };
        }
    }
}