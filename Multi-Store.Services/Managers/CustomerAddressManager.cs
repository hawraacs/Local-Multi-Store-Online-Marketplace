using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class CustomerAddressManager
    {
        private readonly ICustomerAddressRepository _addressRepository;

        public CustomerAddressManager(ICustomerAddressRepository addressRepository)
        {
            _addressRepository = addressRepository;
        }

        // GET ALL ADDRESSES
        public async Task<List<CustomerAddressDTO>> GetCustomerAddressesAsync(int customerId)
        {
            var addresses = await _addressRepository.GetByCustomerIdAsync(customerId);

            return addresses.Select(a => new CustomerAddressDTO
            {
                AddressID = a.AddressID,
                CustomerID = a.CustomerID,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                City = a.City,
                Area = a.Area,
                PostalCode = a.PostalCode,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                IsDefault = a.IsDefault,
                IsActive = a.IsActive
            }).ToList();
        }

        // ADD ADDRESS
        public async Task AddAddressAsync(CustomerAddressDTO dto)
        {
            if (dto.IsDefault)
            {
                await _addressRepository.SetAllAsNonDefaultAsync(dto.CustomerID);
            }

            var address = new CustomerAddress
            {
                CustomerID = dto.CustomerID,
                AddressLine1 = dto.AddressLine1,
                AddressLine2 = dto.AddressLine2,
                City = dto.City,
                Area = dto.Area,
                PostalCode = dto.PostalCode,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                IsDefault = dto.IsDefault,
                IsActive = true
            };

            await _addressRepository.AddAsync(address);
        }

        // UPDATE ADDRESS
        public async Task UpdateAddressAsync(CustomerAddressDTO dto)
        {
            var address = await _addressRepository.GetByIdAsync(dto.AddressID);

            if (address == null)
                return;

            if (dto.IsDefault)
            {
                await _addressRepository.SetAllAsNonDefaultAsync(dto.CustomerID);
            }

            address.AddressLine1 = dto.AddressLine1;
            address.AddressLine2 = dto.AddressLine2;
            address.City = dto.City;
            address.Area = dto.Area;
            address.PostalCode = dto.PostalCode;
            address.Latitude = dto.Latitude;
            address.Longitude = dto.Longitude;
            address.IsDefault = dto.IsDefault;
            address.IsActive = dto.IsActive;

            await _addressRepository.UpdateAsync(address);
        }

        // DELETE ADDRESS
        public async Task DeleteAddressAsync(int addressId)
        {
            var address = await _addressRepository.GetByIdAsync(addressId);

            if (address != null)
            {
                await _addressRepository.DeleteAsync(address);
            }
        }

        // SET DEFAULT ADDRESS
        public async Task SetDefaultAddressAsync(int customerId, int addressId)
        {
            await _addressRepository.SetAllAsNonDefaultAsync(customerId);

            var address = await _addressRepository.GetByIdAsync(addressId);

            if (address == null)
                return;

            if (address.CustomerID != customerId)
                return;

            address.IsDefault = true;
            address.IsActive = true;

            await _addressRepository.UpdateAsync(address);
        }
    }
}