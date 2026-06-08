using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class StoreManager
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IMapper _mapper;

        public StoreManager(IStoreRepository storeRepository, IMapper mapper)
        {
            _storeRepository = storeRepository;
            _mapper = mapper;
        }

        // =====================
        // REGISTER STORE
        // =====================
        public async Task<int> RegisterStoreAsync(StoreDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.OwnerUserID <= 0)
                throw new InvalidOperationException("Invalid store owner.");

            if (string.IsNullOrWhiteSpace(dto.StoreName))
                throw new InvalidOperationException("Store name is required.");

            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new InvalidOperationException("Store description is required.");

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                throw new InvalidOperationException("Store phone number is required.");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new InvalidOperationException("Store email is required.");

            if (string.IsNullOrWhiteSpace(dto.AddressLine1))
                throw new InvalidOperationException("Store address is required.");

            if (string.IsNullOrWhiteSpace(dto.City))
                throw new InvalidOperationException("City is required.");

            if (string.IsNullOrWhiteSpace(dto.Area))
                throw new InvalidOperationException("Area is required.");

            if (string.IsNullOrWhiteSpace(dto.BusinessLicenseNumber))
                throw new InvalidOperationException("Business license number is required.");

           

            var existingStore = await _storeRepository.GetByOwnerIdAsync(dto.OwnerUserID);

            if (existingStore != null)
            {
                if (existingStore.Status == "Pending")
                    throw new InvalidOperationException("You already have a pending store request. Please wait for admin approval.");

                if (existingStore.Status == "Approved")
                    throw new InvalidOperationException("You are already a store owner.");

                // If rejected, allow the user to resubmit the store request
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

                // Location fields required for UC-30
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,

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
        // APPROVE STORE
        // =====================
        public async Task<(string email, string password)> ApproveStoreWithAccountAsync(
            int storeId,
            int adminId,
            UserManager<User> userManager)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found");

            var owner = await userManager.FindByIdAsync(store.OwnerUserID.ToString());

            if (owner == null)
                throw new Exception("User not found in AspNetUsers");

            if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
            {
                var roleResult = await userManager.AddToRoleAsync(owner, "StoreOwner");

                if (!roleResult.Succeeded)
                    throw new Exception(string.Join(",", roleResult.Errors.Select(e => e.Description)));
            }

            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);

            return (owner.Email ?? string.Empty, "Use your existing password");
        }

        // =====================
        // CHECK APPROVAL
        // =====================
        public async Task<bool> IsStoreApprovedAsync(int userId)
        {
            var store = await _storeRepository.GetByOwnerIdAsync(userId);

            return store != null && store.Status == "Approved";
        }

        // =====================
        // REJECT STORE
        // =====================
        public async Task RejectStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found");

            store.Status = "Rejected";

            await _storeRepository.UpdateAsync(store);
        }

        // =====================
        // GET ALL STORES
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
                Owner = s.Owner,
                Approver = s.Approver,
                Products = s.Products,
                DeliveryAreas = s.DeliveryAreas,
                Coupons = s.Coupons,
                OrderItems = s.OrderItems,
                Reviews = s.Reviews,
                Complaints = s.Complaints
            });
        }

        // =====================
        // GET NEARBY STORES
        // =====================
        public async Task<List<StoreDTO>> GetNearbyStoresAsync(
            double customerLat,
            double customerLng,
            double radiusKm = 10)
        {
            var stores = await _storeRepository.GetApprovedStoresAsync();

            return stores
                .Where(s => s.Latitude != 0 && s.Longitude != 0)
                .Select(s => new
                {
                    Store = s,
                    Distance = CalculateDistanceKm(
                        customerLat,
                        customerLng,
                        Convert.ToDouble(s.Latitude),
                        Convert.ToDouble(s.Longitude))
                })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Select(x => new StoreDTO
                {
                    StoreID = x.Store.StoreID,
                    OwnerUserID = x.Store.OwnerUserID,
                    StoreName = x.Store.StoreName,
                    StoreCode = x.Store.StoreCode,
                    Description = x.Store.Description,
                    LogoURL = x.Store.LogoURL,
                    PhoneNumber = x.Store.PhoneNumber,
                    Email = x.Store.Email,
                    AddressLine1 = x.Store.AddressLine1,
                    AddressLine2 = x.Store.AddressLine2,
                    City = x.Store.City,
                    Area = x.Store.Area,
                    Latitude = x.Store.Latitude,
                    Longitude = x.Store.Longitude,
                    Rating = x.Store.Rating,
                    TotalRatings = x.Store.TotalRatings,
                    Status = x.Store.Status,
                    DistanceKm = Math.Round(x.Distance, 2)
                })
                .ToList();
        }

        private double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        // =====================
        // ACTIVATE STORE
        // =====================
        public async Task ActivateStoreAsync(int storeId, int adminId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found");

            store.Status = "Approved";

            if (store.ApprovedAt == null)
                store.ApprovedAt = DateTime.UtcNow;

            if (store.ApprovedBy == null)
                store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);
        }

        // =====================
        // DEACTIVATE STORE
        // =====================
        public async Task DeactivateStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found");

            store.Status = "Inactive";

            await _storeRepository.UpdateAsync(store);
        }
    }
}