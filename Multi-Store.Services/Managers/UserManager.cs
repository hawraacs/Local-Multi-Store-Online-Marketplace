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
    public class UserManager
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IMapper _mapper;

        public UserManager(
            IUserRepository userRepository,
            IAuditLogRepository auditLogRepository,
            ISessionRepository sessionRepository,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _auditLogRepository = auditLogRepository;
            _sessionRepository = sessionRepository;
            _mapper = mapper;
        }

        // =========================
        // Get All Users
        // =========================
        public async Task<IEnumerable<UserDTO>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();

            return _mapper.Map<IEnumerable<UserDTO>>(users);
        }

        // =========================
        // Get User By ID
        // =========================
        public async Task<UserDTO?> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);

            return user == null ? null : _mapper.Map<UserDTO>(user);
        }

        // =========================
        // Get By Email
        // =========================
        public async Task<UserDTO?> GetByEmailAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);

            return user == null ? null : _mapper.Map<UserDTO>(user);
        }

        // =========================
        // Get By Phone
        // =========================
        public async Task<UserDTO?> GetByPhoneAsync(string phone)
        {
            var user = await _userRepository.GetByPhoneAsync(phone);

            return user == null ? null : _mapper.Map<UserDTO>(user);
        }

        // =========================
        // Get Active Users
        // =========================
        public async Task<IEnumerable<UserDTO>> GetActiveUsersAsync()
        {
            var users = await _userRepository.GetActiveUsersAsync();

            return _mapper.Map<IEnumerable<UserDTO>>(users);
        }

        // =========================
        // Add User
        // =========================
        public async Task<string> AddUserAsync(
            UserDTO userDTO,
            string ipAddress,
            string userAgent)
        {
            if (await _userRepository.EmailExistsAsync(userDTO.Email))
                throw new Exception("Email already exists.");

            if (await _userRepository.PhoneExistsAsync(userDTO.PhoneNumber))
                throw new Exception("Phone number already exists.");

            var user = _mapper.Map<User>(userDTO);

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            await _userRepository.AddAsync(user);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = user.Id,
                Action = "Register",
                EntityName = "User",
                EntityID = user.Id.ToString(),
                OldValue = null,
                NewValue = $"User {user.Email} registered.",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return user.Id.ToString();
        }

        // =========================
        // Update User
        // =========================
        public async Task UpdateUserAsync(
            UserDTO userDTO,
            string ipAddress,
            string userAgent)
        {
            var existingUser = await _userRepository.GetByIdAsync(userDTO.UserID);

            if (existingUser == null)
                throw new Exception("User not found.");

            var oldValue =
                $"Name:{existingUser.FullName}," +
                $" Email:{existingUser.Email}," +
                $" Phone:{existingUser.PhoneNumber}";

            _mapper.Map(userDTO, existingUser);

            await _userRepository.UpdateAsync(existingUser);

            var newValue =
                $"Name:{existingUser.FullName}," +
                $" Email:{existingUser.Email}," +
                $" Phone:{existingUser.PhoneNumber}";

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = existingUser.Id,
                Action = "Update",
                EntityName = "User",
                EntityID = existingUser.Id.ToString(),
                OldValue = oldValue,
                NewValue = newValue,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Delete User
        // =========================
        public async Task DeleteUserAsync(
            int id,
            string ipAddress,
            string userAgent)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                throw new Exception("User not found.");

            await _userRepository.DeleteAsync(user);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = user.Id,
                Action = "Delete",
                EntityName = "User",
                EntityID = user.Id.ToString(),
                OldValue = user.Email,
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Logout
        // =========================
        public async Task LogoutAsync(
            string sessionToken,
            string ipAddress,
            string userAgent)
        {
            var session = await _sessionRepository.GetByTokenAsync(sessionToken);

            if (session == null)
                throw new Exception("Session not found.");

            session.IsActive = false;

            await _sessionRepository.UpdateAsync(session);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = session.UserID,
                Action = "Logout",
                EntityName = "Session",
                
                OldValue = "Active",
                NewValue = "Inactive",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Check Email Exists
        // =========================
        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _userRepository.EmailExistsAsync(email);
        }

        // =========================
        // Check Phone Exists
        // =========================
        public async Task<bool> PhoneExistsAsync(string phone)
        {
            return await _userRepository.PhoneExistsAsync(phone);
        }

        // =========================
        // Block User
        // =========================
        public async Task BlockUserAsync(
            int userId,
            string ipAddress,
            string userAgent)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                throw new Exception("User not found.");

            user.IsActive = false;

            await _userRepository.UpdateAsync(user);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = user.Id,
                Action = "Block",
                EntityName = "User",
                EntityID = user.Id.ToString(),
                OldValue = "Active",
                NewValue = "Blocked",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================
        // Activate User
        // =========================
        public async Task ActivateUserAsync(
           int userId,
            string ipAddress,
            string userAgent)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                throw new Exception("User not found.");

            user.IsActive = true;

            await _userRepository.UpdateAsync(user);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = user.Id,
                Action = "Activate",
                EntityName = "User",
                EntityID = user.Id.ToString(),
                OldValue = "Blocked",
                NewValue = "Active",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }
    }

}
