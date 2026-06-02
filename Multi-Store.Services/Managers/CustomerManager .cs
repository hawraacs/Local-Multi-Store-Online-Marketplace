using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;


namespace Multi_Store.Services.Managers
{
    public class CustomerManager
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerAddressRepository _customerAddressRepository;
        private readonly IWishlistRepository _wishlistRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IMapper _mapper;

        public CustomerManager(
            ICustomerRepository customerRepository,
            ICustomerAddressRepository customerAddressRepository,
            IWishlistRepository wishlistRepository,
            IAuditLogRepository auditLogRepository,
            IMapper mapper)
        {
            _customerRepository = customerRepository;
            _customerAddressRepository = customerAddressRepository;
            _wishlistRepository = wishlistRepository;
            _auditLogRepository = auditLogRepository;
            _mapper = mapper;
        }

        // =========================
        // Get All Customers
        // =========================
        public async Task<IEnumerable<CustomerDTO>> GetAllCustomersAsync()
        {
            var customers = await _customerRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<CustomerDTO>>(customers);
        }

        // =========================
        // Get Customer By ID
        // =========================
        public async Task<CustomerDTO?> GetCustomerByIdAsync(int id)
        {
            var customer = await _customerRepository.GetByIdAsync(id);
            return _mapper.Map<CustomerDTO?>(customer);
        }

        // =========================
        // Get Customer By User ID
        // =========================
        public async Task<CustomerDTO?> GetCustomerByUserIdAsync(int userId)
        {
            var customer = await _customerRepository.GetByUserIdAsync(userId);
            return _mapper.Map<CustomerDTO?>(customer);
        }

        // =========================
        // Get Customer With Addresses
        // =========================
        public async Task<CustomerDTO?> GetCustomerWithAddressesAsync(int customerId)
        {
            var customer = await _customerRepository.GetWithAddressesAsync(customerId);
            return _mapper.Map<CustomerDTO?>(customer);
        }

        // =========================
        // Get Customer With Orders
        // =========================
        public async Task<CustomerDTO?> GetCustomerWithOrdersAsync(int customerId)
        {
            var customer = await _customerRepository.GetWithOrdersAsync(customerId);
            return _mapper.Map<CustomerDTO?>(customer);
        }

        // =========================
        // Get Top Customers
        // =========================
        public async Task<IEnumerable<CustomerDTO>> GetTopCustomersAsync(int count)
        {
            var customers = await _customerRepository.GetTopCustomersAsync(count);
            return _mapper.Map<IEnumerable<CustomerDTO>>(customers);
        }

        // =========================
        // Get Blocked COD Customers
        // =========================
        public async Task<IEnumerable<CustomerDTO>> GetBlockedCODCustomersAsync()
        {
            var customers = await _customerRepository.GetBlockedCODCustomersAsync();
            return _mapper.Map<IEnumerable<CustomerDTO>>(customers);
        }

        // =========================
        // Add Customer
        // =========================
        public async Task<int> AddCustomerAsync(
            CustomerDTO customerDTO,
            string ipAddress,
            string userAgent)
        {
            var existingCustomer = await _customerRepository.GetByUserIdAsync(customerDTO.UserID);
            if (existingCustomer != null)
                throw new Exception("Customer profile already exists for this user.");

            var customer = _mapper.Map<Customer>(customerDTO);
            customer.IsVerified = false;
            customer.LoyaltyPoints = 0;
            customer.CODBlocked = false;

            await _customerRepository.AddAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "AddCustomer",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = null,
                NewValue = $"Customer profile created for UserID: {customer.UserID}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return customer.CustomerID;
        }

        // =========================
        // Update Customer
        // =========================
        public async Task UpdateCustomerAsync(
            CustomerDTO customerDTO,
            string ipAddress,
            string userAgent)
        {
            var existingCustomer = await _customerRepository.GetByIdAsync(customerDTO.CustomerID);

            if (existingCustomer == null)
                throw new Exception("Customer not found.");

            existingCustomer.DateOfBirth = customerDTO.DateOfBirth;
            existingCustomer.Gender = customerDTO.Gender;
            existingCustomer.DefaultAddressID = customerDTO.DefaultAddressID;

            await _customerRepository.UpdateAsync(existingCustomer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = existingCustomer.UserID,
                Action = "UpdateCustomer",
                EntityName = "Customer",
                EntityID = existingCustomer.CustomerID.ToString(),
                OldValue = null,
                NewValue = "Customer updated",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Delete Customer
        // =========================
        public async Task DeleteCustomerAsync(
            int id,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(id);

            if (customer == null)
                throw new Exception("Customer not found.");

            await _customerRepository.DeleteAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "DeleteCustomer",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = "Active",
                NewValue = "Deleted",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Verify Customer
        // =========================
        public async Task VerifyCustomerAsync(
            int customerId,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            var oldValue = $"IsVerified: {customer.IsVerified}";
            customer.IsVerified = true;

            await _customerRepository.UpdateAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "VerifyCustomer",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = oldValue,
                NewValue = "IsVerified: True",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Block COD for Customer
        // =========================
        public async Task BlockCODAsync(
            int customerId,
            string reason,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            var oldValue = $"CODBlocked: {customer.CODBlocked}";
            customer.CODBlocked = true;

            await _customerRepository.UpdateAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "BlockCOD",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = oldValue,
                NewValue = $"CODBlocked: True, Reason: {reason}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Unblock COD for Customer
        // =========================
        public async Task UnblockCODAsync(
            int customerId,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            var oldValue = $"CODBlocked: {customer.CODBlocked}";
            customer.CODBlocked = false;

            await _customerRepository.UpdateAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "UnblockCOD",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = oldValue,
                NewValue = "CODBlocked: False",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Add Loyalty Points
        // =========================
        public async Task AddLoyaltyPointsAsync(
            int customerId,
            int points,
            string reason,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            var oldValue = $"LoyaltyPoints: {customer.LoyaltyPoints}";
            customer.LoyaltyPoints += points;

            await _customerRepository.UpdateAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "AddLoyaltyPoints",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = oldValue,
                NewValue = $"LoyaltyPoints: {customer.LoyaltyPoints}, Reason: {reason}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Deduct Loyalty Points
        // =========================
        public async Task DeductLoyaltyPointsAsync(
            int customerId,
            int points,
            string reason,
            string ipAddress,
            string userAgent)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            if (customer.LoyaltyPoints < points)
                throw new Exception("Insufficient loyalty points.");

            var oldValue = $"LoyaltyPoints: {customer.LoyaltyPoints}";
            customer.LoyaltyPoints -= points;

            await _customerRepository.UpdateAsync(customer);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "DeductLoyaltyPoints",
                EntityName = "Customer",
                EntityID = customer.CustomerID.ToString(),
                OldValue = oldValue,
                NewValue = $"LoyaltyPoints: {customer.LoyaltyPoints}, Reason: {reason}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Get All Addresses for Customer
        // =========================
        public async Task<IEnumerable<CustomerAddressDTO>> GetAddressesByCustomerIdAsync(int customerId)
        {
            var addresses = await _customerAddressRepository.GetByCustomerIdAsync(customerId);
            return _mapper.Map<IEnumerable<CustomerAddressDTO>>(addresses);
        }

        // =========================
        // Get Default Address
        // =========================
        public async Task<CustomerAddressDTO?> GetDefaultAddressAsync(int customerId)
        {
            var address = await _customerAddressRepository.GetDefaultAddressAsync(customerId);
            return _mapper.Map<CustomerAddressDTO?>(address);
        }

        // =========================
        // Get Active Addresses
        // =========================
        public async Task<IEnumerable<CustomerAddressDTO>> GetActiveAddressesAsync(int customerId)
        {
            var addresses = await _customerAddressRepository.GetActiveAddressesAsync(customerId);
            return _mapper.Map<IEnumerable<CustomerAddressDTO>>(addresses);
        }

        // =========================
        // Add Address
        // =========================
        public async Task<int> AddAddressAsync(
            CustomerAddressDTO addressDTO,
            string ipAddress,
            string userAgent)
        {
            if (addressDTO.IsDefault)
            {
                await _customerAddressRepository.SetAllAsNonDefaultAsync(addressDTO.CustomerID);
            }

            var address = _mapper.Map<CustomerAddress>(addressDTO);
            address.IsActive = true;

            await _customerAddressRepository.AddAsync(address);

            if (addressDTO.IsDefault)
            {
                await UpdateCustomerDefaultAddress(addressDTO.CustomerID, address.AddressID);
            }

            var customer = await _customerRepository.GetByIdAsync(addressDTO.CustomerID);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "AddAddress",
                EntityName = "CustomerAddress",
                EntityID = address.AddressID.ToString(),
                OldValue = null,
                NewValue = $"Address added: {address.AddressLine1}, {address.City}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return address.AddressID;
        }

        // =========================
        // Update Address
        // =========================
        public async Task UpdateAddressAsync(
            CustomerAddressDTO addressDTO,
            string ipAddress,
            string userAgent)
        {
            var existingAddress = await _customerAddressRepository.GetByIdAsync(addressDTO.AddressID);

            if (existingAddress == null)
                throw new Exception("Address not found.");

            var oldValue = $"{existingAddress.AddressLine1}, {existingAddress.City}";

            if (addressDTO.IsDefault && !existingAddress.IsDefault)
            {
                await _customerAddressRepository.SetAllAsNonDefaultAsync(addressDTO.CustomerID);
            }

            _mapper.Map(addressDTO, existingAddress);

            await _customerAddressRepository.UpdateAsync(existingAddress);

            if (addressDTO.IsDefault)
            {
                await UpdateCustomerDefaultAddress(addressDTO.CustomerID, addressDTO.AddressID);
            }
            else if (!addressDTO.IsDefault && existingAddress.IsDefault != addressDTO.IsDefault)
            {
                var customer = await _customerRepository.GetByIdAsync(addressDTO.CustomerID);
                if (customer?.DefaultAddressID == addressDTO.AddressID)
                {
                    customer.DefaultAddressID = null;
                    await _customerRepository.UpdateAsync(customer);
                }
            }

            var newValue = $"{existingAddress.AddressLine1}, {existingAddress.City}";
            var customerLog = await _customerRepository.GetByIdAsync(addressDTO.CustomerID);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customerLog?.UserID ?? 0,
                Action = "UpdateAddress",
                EntityName = "CustomerAddress",
                EntityID = existingAddress.AddressID.ToString(),
                OldValue = oldValue,
                NewValue = newValue,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Delete Address
        // =========================
        public async Task DeleteAddressAsync(
            int addressId,
            string ipAddress,
            string userAgent)
        {
            var address = await _customerAddressRepository.GetByIdAsync(addressId);

            if (address == null)
                throw new Exception("Address not found.");

            var customer = await _customerRepository.GetByIdAsync(address.CustomerID);

            if (customer?.DefaultAddressID == addressId)
            {
                customer.DefaultAddressID = null;
                await _customerRepository.UpdateAsync(customer);
            }

            await _customerAddressRepository.DeleteAsync(address);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "DeleteAddress",
                EntityName = "CustomerAddress",
                EntityID = address.AddressID.ToString(),
                OldValue = $"{address.AddressLine1}, {address.City}",
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Helper: Update Customer Default Address
        // =========================
        private async Task UpdateCustomerDefaultAddress(int customerId, int addressId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer != null)
            {
                customer.DefaultAddressID = addressId;
                await _customerRepository.UpdateAsync(customer);
            }
        }

        // =========================
        // Check Methods
        // =========================
        public async Task<bool> CustomerExistsAsync(int customerId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            return customer != null;
        }

        public async Task<bool> IsCustomerVerifiedAsync(int customerId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            return customer?.IsVerified ?? false;
        }

        public async Task<bool> IsCODBlockedAsync(int customerId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            return customer?.CODBlocked ?? false;
        }

        // =========================
        // Count Methods
        // =========================
        public async Task<int> GetTotalCustomerCountAsync()
        {
            var customers = await _customerRepository.GetAllAsync();
            return customers.Count();
        }

        public async Task<int> GetVerifiedCustomerCountAsync()
        {
            var customers = await _customerRepository.GetAllAsync();
            return customers.Count(c => c.IsVerified);
        }
        public async Task SetDefaultAddressAsync(int customerId, int addressId)
        {
            await _customerAddressRepository.SetAllAsNonDefaultAsync(customerId);

            var address = await _customerAddressRepository.GetByIdAsync(addressId);

            if (address == null)
                return;

            if (address.CustomerID != customerId)
                return;

            address.IsDefault = true;
            address.IsActive = true;

            await _customerAddressRepository.UpdateAsync(address);

            await UpdateCustomerDefaultAddress(customerId, addressId);
        }

        // =========================
        // NOTE: Order methods removed because IOrderRepository doesn't have GetByCustomerIdAsync
        // Add these methods to IOrderRepository when needed:
        // Task<IReadOnlyList<Order>> GetByCustomerIdAsync(int customerId);
        // =========================
    }
}