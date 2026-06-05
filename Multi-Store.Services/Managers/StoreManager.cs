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
            // ✅ Check if user already has a pending or approved store
            var existingStore = await _storeRepository.GetByOwnerIdAsync(dto.OwnerUserID);
            if (existingStore != null)
            {
                if (existingStore.Status == "Pending")
                    throw new InvalidOperationException("You already have a pending store request. Please wait for admin approval.");
                if (existingStore.Status == "Approved")
                    throw new InvalidOperationException("You are already a store owner.");
                // If Status == "Rejected", allow new request
            }

            var store = new Store
            {
                OwnerUserID = dto.OwnerUserID,
                StoreName = dto.StoreName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                AddressLine1 = dto.AddressLine1,
                City = dto.City,
                Area = dto.Area,
                Description = dto.Description,
                BusinessLicenseNumber = dto.BusinessLicenseNumber,

                StoreCode = "ST-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"Saving Store: {store.StoreName}, Owner: {store.OwnerUserID}");

            var saved = await _storeRepository.AddAsync(store);

            Console.WriteLine($"Saved Store ID: {saved.StoreID}");

            return saved.StoreID;
        }

        // =====================
        // APPROVE STORE (NO EMAIL/PASSWORD CHANGE)
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

            // ✅ DO NOT change the user's email or password
            // ✅ Only add the StoreOwner role if not already present

            if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
            {
                var roleResult = await userManager.AddToRoleAsync(owner, "StoreOwner");
                if (!roleResult.Succeeded)
                    throw new Exception(string.Join(",", roleResult.Errors.Select(e => e.Description)));
            }

            // Approve store
            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);

            // Return the existing email (no new password needed)
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
            return _mapper.Map<IEnumerable<StoreDTO>>(stores);
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