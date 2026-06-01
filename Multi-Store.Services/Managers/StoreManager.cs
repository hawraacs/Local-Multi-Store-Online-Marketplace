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
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public StoreManager(
            IStoreRepository storeRepository,
            IUserRepository userRepository,
            IMapper mapper)
        {
            _storeRepository = storeRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        // =========================
        // REGISTER STORE REQUEST
        // =========================
        public async Task<int> RegisterStoreAsync(StoreDTO dto)
        {
            var store = _mapper.Map<Store>(dto);

            store.Status = "Pending"; // FORCE IT
            store.CreatedAt = DateTime.UtcNow;

            var saved = await _storeRepository.AddAsync(store);

            return saved.StoreID;
        }
        // =========================
        // APPROVE + CREATE ACCOUNT
        // =========================
        public async Task<(string email, string password)> ApproveStoreWithAccountAsync(
        int storeId,
        int adminId,
        UserManager<User> userManager)
        {
            // 1. Get store
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found");

            // 2. Find existing Identity user (IMPORTANT)
            var owner = await userManager.FindByEmailAsync(store.Email.Trim());

            if (owner == null)
                throw new Exception("User not found in AspNetUsers");

            // 3. Generate password
            string password = $"Store@{store.StoreID}2026";

            // 4. Reset password (SAFE Identity way)
            var token = await userManager.GeneratePasswordResetTokenAsync(owner);

            var resetResult = await userManager.ResetPasswordAsync(owner, token, password);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                throw new Exception("Password reset failed: " + errors);
            }

            // 5. Update user info (optional but safe)
            owner.UserName = store.Email.Trim();
            owner.Email = store.Email.Trim();

            await userManager.UpdateAsync(owner);

            // 6. Approve store
            store.OwnerUserID = owner.Id;
            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);

            // 7. Return credentials to admin UI
            return (owner.Email, password);
        }

        public async Task<bool> IsStoreApprovedAsync(int userId)
        {
            var store = await _storeRepository.GetByOwnerIdAsync(userId);

            return store != null && store.Status == "Approved";
        }
        // =========================
        // REJECT
        // =========================
        public async Task RejectStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            store.Status = "Rejected";

            await _storeRepository.UpdateAsync(store);
        }

        // =========================
        // GET ALL
        // =========================
        public async Task<IEnumerable<StoreDTO>> GetAllStoresAsync()
        {
            var stores = await _storeRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<StoreDTO>>(stores);
        }
    }
}