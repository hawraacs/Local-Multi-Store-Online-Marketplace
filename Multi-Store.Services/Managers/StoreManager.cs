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
        // APPROVE STORE + ROLE + PASSWORD
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

            // password
            string password = $"Store@{store.StoreID}2026";

            // reset password safely
            var token = await userManager.GeneratePasswordResetTokenAsync(owner);
            var reset = await userManager.ResetPasswordAsync(owner, token, password);

            if (!reset.Succeeded)
                throw new Exception(string.Join(",", reset.Errors.Select(e => e.Description)));

            // update login info
            owner.Email = store.Email?.Trim();
            owner.UserName = store.Email?.Trim();

            await userManager.UpdateAsync(owner);

            // IMPORTANT: assign role
            if (!await userManager.IsInRoleAsync(owner, "StoreOwner"))
                await userManager.AddToRoleAsync(owner, "StoreOwner");

            // approve store
            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);

            return (owner.Email, password);
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
    }
}