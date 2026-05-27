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
    public class ComplaintManager
    {
        private readonly IComplaintRepository _complaintRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IMapper _mapper;

        public ComplaintManager(
            IComplaintRepository complaintRepository,
            ICustomerRepository customerRepository,
            IAuditLogRepository auditLogRepository,
            IMapper mapper)
        {
            _complaintRepository = complaintRepository;
            _customerRepository = customerRepository;
            _auditLogRepository = auditLogRepository;
            _mapper = mapper;
        }

        // =========================================================
        // 1. BASIC CRUD OPERATIONS
        // =========================================================

        /// <summary>
        /// Get all complaints
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetAllComplaintsAsync()
        {
            var complaints = await _complaintRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get complaint by ID
        /// </summary>
        public async Task<ComplaintDTO?> GetComplaintByIdAsync(int complaintId)
        {
            var complaint = await _complaintRepository.GetByIdAsync(complaintId);
            return _mapper.Map<ComplaintDTO?>(complaint);
        }

        /// <summary>
        /// Add new complaint
        /// </summary>
        public async Task<int> AddComplaintAsync(
            ComplaintDTO complaintDTO,
            string ipAddress,
            string userAgent)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(complaintDTO.ComplaintType))
                throw new Exception("Complaint type is required.");

            if (string.IsNullOrWhiteSpace(complaintDTO.Description))
                throw new Exception("Description is required.");

            var customer = await _customerRepository.GetByIdAsync(complaintDTO.CustomerID);

            if (customer == null)
                throw new Exception("Customer not found.");

            var complaint = _mapper.Map<Complaint>(complaintDTO);

            complaint.Status = "Pending";
            complaint.CreatedAt = DateTime.UtcNow;
            complaint.ResolvedAt = null;

            await _complaintRepository.AddAsync(complaint);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer.UserID,
                Action = "AddComplaint",
                EntityName = "Complaint",
                EntityID = complaint.ComplaintID.ToString(),
                OldValue = null,
                NewValue = $"Complaint created. Type: {complaint.ComplaintType}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            return complaint.ComplaintID;
        }

        /// <summary>
        /// Update complaint
        /// </summary>
        public async Task UpdateComplaintAsync(
            ComplaintDTO complaintDTO,
            string ipAddress,
            string userAgent)
        {
            var existingComplaint =
                await _complaintRepository.GetByIdAsync(complaintDTO.ComplaintID);

            if (existingComplaint == null)
                throw new Exception("Complaint not found.");

            var oldValue =
                $"Type:{existingComplaint.ComplaintType}, " +
                $"Status:{existingComplaint.Status}";

            existingComplaint.ComplaintType = complaintDTO.ComplaintType;
            existingComplaint.Description = complaintDTO.Description;
            existingComplaint.EvidenceImageURL = complaintDTO.EvidenceImageURL;
            existingComplaint.AdminNotes = complaintDTO.AdminNotes;

            await _complaintRepository.UpdateAsync(existingComplaint);

            var customer =
                await _customerRepository.GetByIdAsync(existingComplaint.CustomerID);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "UpdateComplaint",
                EntityName = "Complaint",
                EntityID = existingComplaint.ComplaintID.ToString(),
                OldValue = oldValue,
                NewValue =
                    $"Type:{existingComplaint.ComplaintType}, " +
                    $"Status:{existingComplaint.Status}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Delete complaint
        /// </summary>
        public async Task DeleteComplaintAsync(
            int complaintId,
            string ipAddress,
            string userAgent)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            if (complaint == null)
                throw new Exception("Complaint not found.");

            await _complaintRepository.DeleteAsync(complaint);

            var customer =
                await _customerRepository.GetByIdAsync(complaint.CustomerID);

            // Audit Log
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "DeleteComplaint",
                EntityName = "Complaint",
                EntityID = complaint.ComplaintID.ToString(),
                OldValue = $"Complaint Type: {complaint.ComplaintType}",
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // 2. GET COMPLAINTS BY FILTER
        // =========================================================

        /// <summary>
        /// Get complaints by customer
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetComplaintsByCustomerAsync(int customerId)
        {
            var complaints =
                await _complaintRepository.GetByCustomerAsync(customerId);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get complaints by status
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetComplaintsByStatusAsync(string status)
        {
            var complaints =
                await _complaintRepository.GetByStatusAsync(status);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get complaints by order
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetComplaintsByOrderAsync(int orderId)
        {
            var complaints =
                await _complaintRepository.GetByOrderAsync(orderId);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get complaints by product
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetComplaintsByProductAsync(int productId)
        {
            var complaints =
                await _complaintRepository.GetByProductAsync(productId);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get complaints by store
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetComplaintsByStoreAsync(int storeId)
        {
            var complaints =
                await _complaintRepository.GetByStoreAsync(storeId);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        /// <summary>
        /// Get latest complaints
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> GetLatestComplaintsAsync(int count)
        {
            var complaints =
                await _complaintRepository.GetLatestAsync(count);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }

        // =========================================================
        // 3. STATUS MANAGEMENT
        // =========================================================

        /// <summary>
        /// Mark complaint as in progress
        /// </summary>
        public async Task MarkAsInProgressAsync(
            int complaintId,
            string adminNotes,
            string ipAddress,
            string userAgent)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            if (complaint == null)
                throw new Exception("Complaint not found.");

            var oldValue = complaint.Status;

            complaint.Status = "In Progress";
            complaint.AdminNotes = adminNotes;

            await _complaintRepository.UpdateAsync(complaint);

            var customer =
                await _customerRepository.GetByIdAsync(complaint.CustomerID);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "MarkComplaintInProgress",
                EntityName = "Complaint",
                EntityID = complaint.ComplaintID.ToString(),
                OldValue = oldValue,
                NewValue = complaint.Status,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Resolve complaint
        /// </summary>
        public async Task ResolveComplaintAsync(
            int complaintId,
            string resolution,
            string adminNotes,
            string ipAddress,
            string userAgent)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            if (complaint == null)
                throw new Exception("Complaint not found.");

            var oldValue = complaint.Status;

            complaint.Status = "Resolved";
            complaint.Resolution = resolution;
            complaint.AdminNotes = adminNotes;
            complaint.ResolvedAt = DateTime.UtcNow;

            await _complaintRepository.UpdateAsync(complaint);

            var customer =
                await _customerRepository.GetByIdAsync(complaint.CustomerID);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "ResolveComplaint",
                EntityName = "Complaint",
                EntityID = complaint.ComplaintID.ToString(),
                OldValue = oldValue,
                NewValue = "Resolved",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Reject complaint
        /// </summary>
        public async Task RejectComplaintAsync(
            int complaintId,
            string adminNotes,
            string ipAddress,
            string userAgent)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            if (complaint == null)
                throw new Exception("Complaint not found.");

            var oldValue = complaint.Status;

            complaint.Status = "Rejected";
            complaint.AdminNotes = adminNotes;
            complaint.ResolvedAt = DateTime.UtcNow;

            await _complaintRepository.UpdateAsync(complaint);

            var customer =
                await _customerRepository.GetByIdAsync(complaint.CustomerID);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customer?.UserID ?? 0,
                Action = "RejectComplaint",
                EntityName = "Complaint",
                EntityID = complaint.ComplaintID.ToString(),
                OldValue = oldValue,
                NewValue = "Rejected",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        // =========================================================
        // 4. CHECK METHODS
        // =========================================================

        /// <summary>
        /// Check if complaint exists
        /// </summary>
        public async Task<bool> ComplaintExistsAsync(int complaintId)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            return complaint != null;
        }

        /// <summary>
        /// Check if complaint resolved
        /// </summary>
        public async Task<bool> IsComplaintResolvedAsync(int complaintId)
        {
            var complaint =
                await _complaintRepository.GetByIdAsync(complaintId);

            return complaint?.Status == "Resolved";
        }

        // =========================================================
        // 5. COUNT METHODS
        // =========================================================

        /// <summary>
        /// Get total complaints count
        /// </summary>
        public async Task<int> GetTotalComplaintCountAsync()
        {
            var complaints = await _complaintRepository.GetAllAsync();
            return complaints.Count();
        }

        /// <summary>
        /// Get pending complaints count
        /// </summary>
        public async Task<int> GetPendingComplaintCountAsync()
        {
            var complaints =
                await _complaintRepository.GetByStatusAsync("Pending");

            return complaints.Count();
        }

        /// <summary>
        /// Get resolved complaints count
        /// </summary>
        public async Task<int> GetResolvedComplaintCountAsync()
        {
            var complaints =
                await _complaintRepository.GetByStatusAsync("Resolved");

            return complaints.Count();
        }

        /// <summary>
        /// Get rejected complaints count
        /// </summary>
        public async Task<int> GetRejectedComplaintCountAsync()
        {
            var complaints =
                await _complaintRepository.GetByStatusAsync("Rejected");

            return complaints.Count();
        }

        // =========================================================
        // 6. SEARCH METHODS
        // =========================================================

        /// <summary>
        /// Search complaints by complaint type
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> SearchByTypeAsync(string complaintType)
        {
            var complaints = await _complaintRepository.GetAllAsync();

            var filtered = complaints
                .Where(c =>
                    c.ComplaintType.Contains(
                        complaintType,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<ComplaintDTO>>(filtered);
        }

        /// <summary>
        /// Search complaints by description
        /// </summary>
        public async Task<IEnumerable<ComplaintDTO>> SearchByDescriptionAsync(string keyword)
        {
            var complaints = await _complaintRepository.GetAllAsync();

            var filtered = complaints
                .Where(c =>
                    c.Description.Contains(
                        keyword,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<ComplaintDTO>>(filtered);
        }
    }
}