using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class StoreManager
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IDeliveryAreaRepository _deliveryAreaRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public StoreManager(
            IStoreRepository storeRepository,
            IDeliveryAreaRepository deliveryAreaRepository,
            ICouponRepository couponRepository,
            IUserRepository userRepository,
            IMapper mapper)
        {
            _storeRepository = storeRepository;
            _deliveryAreaRepository = deliveryAreaRepository;
            _couponRepository = couponRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        // =========================
        // Store Registration
        // =========================

        public async Task<int> RegisterStoreAsync(StoreDTO storeDTO)
        {
            var owner = await _userRepository.GetByIdAsync(storeDTO.OwnerUserID);

            if (owner == null)
                throw new Exception("Store owner not found.");

            var store = _mapper.Map<Store>(storeDTO);

            store.Status = "Pending";
            store.CreatedAt = DateTime.UtcNow;

            await _storeRepository.AddAsync(store);

            return store.StoreID;
        }

        // =========================
        // Store Approval
        // =========================

        public async Task ApproveStoreAsync(int storeId, int adminId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            store.Status = "Approved";
            store.ApprovedAt = DateTime.UtcNow;
            store.ApprovedBy = adminId;

            await _storeRepository.UpdateAsync(store);
        }

        public async Task RejectStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            store.Status = "Rejected";

            await _storeRepository.UpdateAsync(store);
        }

        // =========================
        // Store Settings
        // =========================

        public async Task UpdateStoreSettingsAsync(StoreDTO storeDTO)
        {
            var existingStore = await _storeRepository.GetByIdAsync(storeDTO.StoreID);

            if (existingStore == null)
                throw new Exception("Store not found.");

            existingStore.StoreName = storeDTO.StoreName;
            existingStore.Description = storeDTO.Description;
            existingStore.PhoneNumber = storeDTO.PhoneNumber;
            existingStore.Email = storeDTO.Email;
            existingStore.AddressLine1 = storeDTO.AddressLine1;
            existingStore.AddressLine2 = storeDTO.AddressLine2;
            existingStore.City = storeDTO.City;
            existingStore.Area = storeDTO.Area;
            existingStore.LogoURL = storeDTO.LogoURL;
            existingStore.CODSupported = storeDTO.CODSupported;
            existingStore.CODMaxLimit = storeDTO.CODMaxLimit;

            await _storeRepository.UpdateAsync(existingStore);
        }

        // =========================
        // Activation / Deactivation
        // =========================

        public async Task ActivateStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            store.Status = "Approved";

            await _storeRepository.UpdateAsync(store);
        }

        public async Task DeactivateStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            store.Status = "Inactive";

            await _storeRepository.UpdateAsync(store);
        }

        // =========================
        // Store Retrieval
        // =========================

        public async Task<IEnumerable<StoreDTO>> GetAllStoresAsync()
        {
            var stores = await _storeRepository.GetAllAsync();

            return _mapper.Map<IEnumerable<StoreDTO>>(stores);
        }

        public async Task<StoreDTO?> GetStoreByIdAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                return null;

            return _mapper.Map<StoreDTO>(store);
        }

        public async Task<StoreDTO?> GetStoreDetailsAsync(int storeId)
        {
            var store = await _storeRepository.GetStoreDetailsAsync(storeId);

            if (store == null)
                return null;

            return _mapper.Map<StoreDTO>(store);
        }

        public async Task<IEnumerable<StoreDTO>> GetApprovedStoresAsync()
        {
            var stores = await _storeRepository.GetApprovedStoresAsync();

            return _mapper.Map<IEnumerable<StoreDTO>>(stores);
        }

        public async Task<IEnumerable<StoreDTO>> SearchStoresAsync(string keyword)
        {
            var stores = await _storeRepository.SearchStoresAsync(keyword);

            return _mapper.Map<IEnumerable<StoreDTO>>(stores);
        }

        // =========================
        // Delivery Areas
        // =========================

        public async Task<IEnumerable<DeliveryArea>> GetStoreDeliveryAreasAsync(int storeId)
        {
            return await _deliveryAreaRepository.GetByStoreAsync(storeId);
        }

        public async Task<IEnumerable<DeliveryArea>> GetActiveDeliveryAreasAsync(int storeId)
        {
            return await _deliveryAreaRepository.GetActiveAreasAsync(storeId);
        }

        // =========================
        // Coupons
        // =========================

        public async Task<IEnumerable<Coupon>> GetStoreCouponsAsync(int storeId)
        {
            return await _couponRepository.GetByStoreAsync(storeId);
        }

        public async Task<IEnumerable<Coupon>> GetActiveStoreCouponsAsync()
        {
            return await _couponRepository.GetActiveCouponsAsync();
        }

        // =========================
        // Delete Store
        // =========================

        public async Task DeleteStoreAsync(int storeId)
        {
            var store = await _storeRepository.GetByIdAsync(storeId);

            if (store == null)
                throw new Exception("Store not found.");

            await _storeRepository.DeleteAsync(store);
        }

        public async Task<bool> IsStoreApprovedAsync(int userId)
{
         var store = await _storeRepository.GetByOwnerIdAsync(userId);

        return store != null && store.Status == "Approved";
}
    }
}
