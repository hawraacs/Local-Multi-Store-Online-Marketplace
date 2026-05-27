using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class AuditLogManager
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public AuditLogManager(
            IAuditLogRepository auditLogRepository,
            IUserRepository userRepository,
            IMapper mapper)
        {
            _auditLogRepository = auditLogRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        // =========================================================
        // 1. BASIC CRUD OPERATIONS
        // =========================================================

        /// <summary>
        /// Get all audit logs
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetAllLogsAsync()
        {
            var logs = await _auditLogRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AuditLogDTO>>(logs);
        }

        /// <summary>
        /// Get audit log by ID
        /// </summary>
        public async Task<AuditLogDTO?> GetLogByIdAsync(int auditLogId)
        {
            var log = await _auditLogRepository.GetByIdAsync(auditLogId);
            return _mapper.Map<AuditLogDTO?>(log);
        }

        /// <summary>
        /// Add new audit log
        /// </summary>
        public async Task<int> AddLogAsync(
            AuditLogDTO auditLogDTO)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(auditLogDTO.Action))
                throw new Exception("Action is required.");

            if (string.IsNullOrWhiteSpace(auditLogDTO.EntityName))
                throw new Exception("Entity name is required.");

            if (string.IsNullOrWhiteSpace(auditLogDTO.EntityID))
                throw new Exception("Entity ID is required.");

            var user = await _userRepository.GetByIdAsync(auditLogDTO.UserID);

            if (user == null)
                throw new Exception("User not found.");

            var auditLog = _mapper.Map<AuditLog>(auditLogDTO);

            auditLog.ActionDate = DateTime.UtcNow;

            await _auditLogRepository.AddAsync(auditLog);

            return auditLog.AuditLogID;
        }

        /// <summary>
        /// Delete audit log
        /// </summary>
        public async Task DeleteLogAsync(int auditLogId)
        {
            var log = await _auditLogRepository.GetByIdAsync(auditLogId);

            if (log == null)
                throw new Exception("Audit log not found.");

            await _auditLogRepository.DeleteAsync(log);
        }

        // =========================================================
        // 2. GET LOGS BY FILTER
        // =========================================================

        /// <summary>
        /// Get logs by user
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetLogsByUserAsync(int userId)
        {
            var logs = await _auditLogRepository.GetLogsByUserAsync(userId);

            return _mapper.Map<IEnumerable<AuditLogDTO>>(logs);
        }

        /// <summary>
        /// Get logs by action
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetLogsByActionAsync(string action)
        {
            var logs = await _auditLogRepository.GetLogsByActionAsync(action);

            return _mapper.Map<IEnumerable<AuditLogDTO>>(logs);
        }

        /// <summary>
        /// Get logs by entity name
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetLogsByEntityAsync(string entityName)
        {
            var logs = await _auditLogRepository.GetAllAsync();

            var filtered = logs
                .Where(l => l.EntityName.Equals(
                    entityName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(filtered);
        }

        /// <summary>
        /// Get logs between dates
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetLogsByDateRangeAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var logs = await _auditLogRepository.GetAllAsync();

            var filtered = logs
                .Where(l => l.ActionDate >= startDate &&
                            l.ActionDate <= endDate)
                .OrderByDescending(l => l.ActionDate)
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(filtered);
        }

        // =========================================================
        // 3. SEARCH METHODS
        // =========================================================

        /// <summary>
        /// Search logs by keyword
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> SearchLogsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllLogsAsync();

            var logs = await _auditLogRepository.GetAllAsync();

            var filtered = logs.Where(l =>
                    l.Action.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    l.EntityName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    l.EntityID.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (l.OldValue != null &&
                     l.OldValue.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (l.NewValue != null &&
                     l.NewValue.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(l => l.ActionDate)
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(filtered);
        }

        // =========================================================
        // 4. CHECK METHODS
        // =========================================================

        /// <summary>
        /// Check if audit log exists
        /// </summary>
        public async Task<bool> LogExistsAsync(int auditLogId)
        {
            var log = await _auditLogRepository.GetByIdAsync(auditLogId);

            return log != null;
        }

        // =========================================================
        // 5. COUNT METHODS
        // =========================================================

        /// <summary>
        /// Get total logs count
        /// </summary>
        public async Task<int> GetTotalLogsCountAsync()
        {
            var logs = await _auditLogRepository.GetAllAsync();

            return logs.Count();
        }

        /// <summary>
        /// Get logs count by user
        /// </summary>
        public async Task<int> GetLogsCountByUserAsync(int userId)
        {
            var logs = await _auditLogRepository.GetLogsByUserAsync(userId);

            return logs.Count();
        }

        /// <summary>
        /// Get logs count by action
        /// </summary>
        public async Task<int> GetLogsCountByActionAsync(string action)
        {
            var logs = await _auditLogRepository.GetLogsByActionAsync(action);

            return logs.Count();
        }

        // =========================================================
        // 6. LATEST LOGS
        // =========================================================

        /// <summary>
        /// Get latest logs
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetLatestLogsAsync(int count)
        {
            var logs = await _auditLogRepository.GetAllAsync();

            var latestLogs = logs
                .OrderByDescending(l => l.ActionDate)
                .Take(count)
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(latestLogs);
        }

        // =========================================================
        // 7. USER ACTIVITY METHODS
        // =========================================================

        /// <summary>
        /// Get latest activity for user
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetUserActivityAsync(
            int userId,
            int count)
        {
            var logs = await _auditLogRepository.GetLogsByUserAsync(userId);

            var latestActivity = logs
                .OrderByDescending(l => l.ActionDate)
                .Take(count)
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(latestActivity);
        }

        /// <summary>
        /// Get failed actions logs
        /// </summary>
        public async Task<IEnumerable<AuditLogDTO>> GetFailedActionsAsync()
        {
            var logs = await _auditLogRepository.GetAllAsync();

            var failedLogs = logs
                .Where(l =>
                    l.Action.Contains("Fail",
                    StringComparison.OrdinalIgnoreCase) ||
                    l.Action.Contains("Error",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.ActionDate)
                .ToList();

            return _mapper.Map<IEnumerable<AuditLogDTO>>(failedLogs);
        }
    }
}