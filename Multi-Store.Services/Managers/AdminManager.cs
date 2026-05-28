using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Repositories;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class AdminManager
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IMapper _mapper;

        public AdminManager(
            IAdminRepository adminRepository,
            IAuditLogRepository auditLogRepository,
            IMapper mapper)
        {
            _adminRepository = adminRepository;
            _auditLogRepository = auditLogRepository;
            _mapper = mapper;
        }

        // =========================================================
        // GET ALL ADMINS
        // =========================================================
        public async Task<IEnumerable<AdminDTO>> GetAllAdminsAsync()
        {
            var admins = await _adminRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AdminDTO>>(admins);
        }

        // =========================================================
        // GET ADMIN BY ID
        // =========================================================
        public async Task<AdminDTO?> GetAdminByIdAsync(int adminId)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);
            return _mapper.Map<AdminDTO?>(admin);
        }

        // =========================================================
        // GET ADMIN BY USER ID
        // =========================================================
        public async Task<AdminDTO?> GetAdminByUserIdAsync(int userId)
        {
            var admins = await _adminRepository.GetAllAsync();

            var admin = admins.FirstOrDefault(a => a.UserID == userId);

            return _mapper.Map<AdminDTO?>(admin);
        }

        // =========================================================
        // ADD ADMIN
        // =========================================================
        public async Task<int> AddAdminAsync(
            AdminDTO adminDTO,
            string ipAddress,
            string userAgent)
        {
            // Check if admin already exists
            var admins = await _adminRepository.GetAllAsync();

            var existingAdmin = admins
                .FirstOrDefault(a => a.UserID == adminDTO.UserID);

            if (existingAdmin != null)
                throw new Exception("Admin already exists for this user.");

            // Map DTO to Entity
            var admin = _mapper.Map<Admin>(adminDTO);

            // Set defaults
            admin.CreatedAt = DateTime.UtcNow;

            await _adminRepository.AddAsync(admin);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = admin.UserID,
                Action = "AddAdmin",
                EntityName = "Admin",
                EntityID = admin.AdminID.ToString(),
                OldValue = null,
                NewValue = $"Admin created with role: {admin.Role}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return admin.AdminID;
        }

        // =========================================================
        // UPDATE ADMIN
        // =========================================================
        public async Task UpdateAdminAsync(
            AdminDTO adminDTO,
            string ipAddress,
            string userAgent)
        {
            var existingAdmin = await _adminRepository.GetByIdAsync(adminDTO.AdminID);

            if (existingAdmin == null)
                throw new Exception("Admin not found.");

            // Save old values
            var oldValue =
                $"Role:{existingAdmin.Role}, " +
                $"Permissions:{existingAdmin.Permissions}";

            // Update fields
            existingAdmin.Role = adminDTO.Role;
            existingAdmin.Permissions = adminDTO.Permissions;

            await _adminRepository.UpdateAsync(existingAdmin);

            // New values
            var newValue =
                $"Role:{existingAdmin.Role}, " +
                $"Permissions:{existingAdmin.Permissions}";

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = existingAdmin.UserID,
                Action = "UpdateAdmin",
                EntityName = "Admin",
                EntityID = existingAdmin.AdminID.ToString(),
                OldValue = oldValue,
                NewValue = newValue,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // DELETE ADMIN
        // =========================================================
        public async Task DeleteAdminAsync(
            int adminId,
            string ipAddress,
            string userAgent)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);

            if (admin == null)
                throw new Exception("Admin not found.");

            await _adminRepository.DeleteAsync(admin);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = admin.UserID,
                Action = "DeleteAdmin",
                EntityName = "Admin",
                EntityID = admin.AdminID.ToString(),
                OldValue = $"Role:{admin.Role}",
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // CHANGE ADMIN ROLE
        // =========================================================
        public async Task ChangeAdminRoleAsync(
            int adminId,
            string newRole,
            string ipAddress,
            string userAgent)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);

            if (admin == null)
                throw new Exception("Admin not found.");

            var oldRole = admin.Role;

            admin.Role = newRole;

            await _adminRepository.UpdateAsync(admin);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = admin.UserID,
                Action = "ChangeAdminRole",
                EntityName = "Admin",
                EntityID = admin.AdminID.ToString(),
                OldValue = oldRole,
                NewValue = newRole,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // UPDATE ADMIN PERMISSIONS
        // =========================================================
        public async Task UpdatePermissionsAsync(
            int adminId,
            string permissions,
            string ipAddress,
            string userAgent)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);

            if (admin == null)
                throw new Exception("Admin not found.");

            var oldPermissions = admin.Permissions;

            admin.Permissions = permissions;

            await _adminRepository.UpdateAsync(admin);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = admin.UserID,
                Action = "UpdatePermissions",
                EntityName = "Admin",
                EntityID = admin.AdminID.ToString(),
                OldValue = oldPermissions,
                NewValue = permissions,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // CHECK IF ADMIN EXISTS
        // =========================================================
        public async Task<bool> AdminExistsAsync(int adminId)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);
            return admin != null;
        }

        // =========================================================
        // CHECK IF USER IS ADMIN
        // =========================================================
        public async Task<bool> IsUserAdminAsync(int userId)
        {
            var admins = await _adminRepository.GetAllAsync();

            return admins.Any(a => a.UserID == userId);
        }

        // =========================================================
        // GET TOTAL ADMINS COUNT
        // =========================================================
        public async Task<int> GetAdminCountAsync()
        {
            var admins = await _adminRepository.GetAllAsync();
            return admins.Count();
        }

        // =========================================================
        // GET ADMINS BY ROLE
        // =========================================================
        public async Task<IEnumerable<AdminDTO>> GetAdminsByRoleAsync(string role)
        {
            var admins = await _adminRepository.GetAllAsync();

            var filteredAdmins = admins
                .Where(a => a.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<AdminDTO>>(filteredAdmins);
        }

        // =========================================================
        // SEARCH ADMINS
        // =========================================================
        public async Task<IEnumerable<AdminDTO>> SearchAdminsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAdminsAsync();

            var admins = await _adminRepository.GetAllAsync();

            var filteredAdmins = admins.Where(a =>
                (a.Role != null &&
                 a.Role.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||

                (a.Permissions != null &&
                 a.Permissions.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            return _mapper.Map<IEnumerable<AdminDTO>>(filteredAdmins);
        }

        // =========================================================
        // GET LATEST ADMINS
        // =========================================================
        public async Task<IEnumerable<AdminDTO>> GetLatestAdminsAsync(int count)
        {
            var admins = await _adminRepository.GetAllAsync();

            var latestAdmins = admins
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToList();

            return _mapper.Map<IEnumerable<AdminDTO>>(latestAdmins);
        }

        // =========================================================
        // GET SUPER ADMINS
        // =========================================================
        public async Task<IEnumerable<AdminDTO>> GetSuperAdminsAsync()
        {
            var admins = await _adminRepository.GetAllAsync();

            var superAdmins = admins
                .Where(a => a.Role == "SuperAdmin")
                .ToList();

            return _mapper.Map<IEnumerable<AdminDTO>>(superAdmins);
        }

        // =========================================================
        // VALIDATE ADMIN PERMISSION
        // =========================================================
        public async Task<bool> HasPermissionAsync(
            int adminId,
            string permission)
        {
            var admin = await _adminRepository.GetByIdAsync(adminId);

            if (admin == null)
                return false;

            if (string.IsNullOrWhiteSpace(admin.Permissions))
                return false;

            return admin.Permissions
                .Contains(permission, StringComparison.OrdinalIgnoreCase);
        }
    }
}