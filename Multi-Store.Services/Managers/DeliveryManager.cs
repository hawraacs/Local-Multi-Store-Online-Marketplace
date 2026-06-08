using Microsoft.AspNetCore.Identity;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class DeliveryManager
    {
        private readonly IDeliveryPersonRepository _deliveryPersonRepository;
        private readonly IDeliveryAssignmentRepository _assignmentRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly UserManager<User> _userManager;

        public DeliveryManager(
            IDeliveryPersonRepository deliveryPersonRepository,
            IDeliveryAssignmentRepository assignmentRepository,
            IOrderRepository orderRepository,
            UserManager<User> userManager)
        {
            _deliveryPersonRepository = deliveryPersonRepository;
            _assignmentRepository = assignmentRepository;
            _orderRepository = orderRepository;
            _userManager = userManager;
        }

        // =========================
        // REGISTER DELIVERY REQUEST
        // =========================
        public async Task<int> RegisterDeliveryPersonAsync(DeliveryPersonDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.UserID <= 0)
                throw new InvalidOperationException("Invalid user.");

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new InvalidOperationException("Full name is required.");

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                throw new InvalidOperationException("Phone number is required.");

            if (string.IsNullOrWhiteSpace(dto.VehicleType))
                throw new InvalidOperationException("Vehicle type is required.");

            if (string.IsNullOrWhiteSpace(dto.VehicleNumber))
                throw new InvalidOperationException("Vehicle number is required.");

            if (string.IsNullOrWhiteSpace(dto.Area))
                throw new InvalidOperationException("Area is required.");

            if (string.IsNullOrWhiteSpace(dto.DrivingLicenseNumber))
                throw new InvalidOperationException("Driving license number is required.");

            if (string.IsNullOrWhiteSpace(dto.IDProofURL))
                throw new InvalidOperationException("ID proof is required.");

            var existing = await _deliveryPersonRepository.GetByUserIdAsync(dto.UserID);

            var phoneExists = await _deliveryPersonRepository.GetByPhoneNumberAsync(dto.PhoneNumber);

            if (phoneExists != null && phoneExists.UserID != dto.UserID)
            {
                throw new InvalidOperationException("This phone number is already registered for another delivery request.");
            }

            if (existing != null)
            {
                if (existing.Status == "Pending")
                    throw new InvalidOperationException("You already have a pending delivery request.");

                if (existing.Status == "Approved")
                    throw new InvalidOperationException("You are already approved as delivery staff.");

                if (existing.Status == "Rejected")
                {
                    existing.FullName = dto.FullName;
                    existing.PhoneNumber = dto.PhoneNumber;
                    existing.Area = dto.Area;
                    existing.VehicleType = dto.VehicleType;
                    existing.VehicleNumber = dto.VehicleNumber;
                    existing.DrivingLicenseNumber = dto.DrivingLicenseNumber;
                    existing.IDProofURL = dto.IDProofURL;
                    existing.RejectionReason = null;
                    existing.Status = "Pending";
                    existing.IsActive = false;
                    existing.ApprovedAt = null;
                    existing.CurrentLatitude = dto.CurrentLatitude;
                    existing.CurrentLongitude = dto.CurrentLongitude;
                    existing.LastLocationUpdate = dto.LastLocationUpdate;

                    await _deliveryPersonRepository.UpdateAsync(existing);

                    return existing.DeliveryPersonID;
                }
            }

            var deliveryPerson = new DeliveryPerson
            {
                UserID = dto.UserID,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                Area = dto.Area,
                VehicleType = dto.VehicleType,
                VehicleNumber = dto.VehicleNumber,
                DrivingLicenseNumber = dto.DrivingLicenseNumber,
                IDProofURL = dto.IDProofURL,
                RejectionReason = null,
                CurrentLatitude = dto.CurrentLatitude,
                CurrentLongitude = dto.CurrentLongitude,
                LastLocationUpdate = dto.LastLocationUpdate,
                Status = "Pending",
                Rating = 0,
                IsActive = false,
                ApprovedAt = null
            };

            var saved = await _deliveryPersonRepository.AddAsync(deliveryPerson);

            return saved.DeliveryPersonID;
        }

        // =========================
        // GET ALL DELIVERY REQUESTS
        // =========================
        public async Task<List<DeliveryPersonDTO>> GetAllAsync()
        {
            var list = await _deliveryPersonRepository.GetAllAsync();

            return list.Select(d => new DeliveryPersonDTO
            {
                DeliveryPersonID = d.DeliveryPersonID,
                UserID = d.UserID,
                FullName = d.FullName,
                PhoneNumber = d.PhoneNumber,
                Area = d.Area,
                VehicleType = d.VehicleType,
                VehicleNumber = d.VehicleNumber,
                DrivingLicenseNumber = d.DrivingLicenseNumber,
                IDProofURL = d.IDProofURL,
                RejectionReason = d.RejectionReason,
                CurrentLatitude = d.CurrentLatitude,
                CurrentLongitude = d.CurrentLongitude,
                LastLocationUpdate = d.LastLocationUpdate,
                Status = d.Status,
                Rating = d.Rating,
                IsActive = d.IsActive,
                ApprovedAt = d.ApprovedAt,
                User = d.User
            }).ToList();
        }

        // =========================
        // GET PENDING DELIVERY REQUESTS
        // =========================
        public async Task<List<DeliveryPersonDTO>> GetPendingDeliveryPersonsAsync()
        {
            var list = await GetAllAsync();

            return list
                .Where(d => !string.IsNullOrWhiteSpace(d.Status) &&
                            d.Status.Trim().Equals("Pending", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.DeliveryPersonID)
                .ToList();
        }

        // =========================
        // APPROVE DELIVERY REQUEST
        // =========================
        public async Task ApproveDeliveryPersonAsync(int deliveryPersonId)
        {
            var deliveryPerson = await _deliveryPersonRepository.GetByIdAsync(deliveryPersonId);

            if (deliveryPerson == null)
                throw new Exception("Delivery request not found.");

            deliveryPerson.Status = "Approved";
            deliveryPerson.IsActive = true;
            deliveryPerson.ApprovedAt = DateTime.UtcNow;
            deliveryPerson.RejectionReason = null;

            await _deliveryPersonRepository.UpdateAsync(deliveryPerson);

            var user = await _userManager.FindByIdAsync(deliveryPerson.UserID.ToString());

            if (user != null && !await _userManager.IsInRoleAsync(user, "Delivery"))
            {
                var result = await _userManager.AddToRoleAsync(user, "Delivery");

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // =========================
        // REJECT DELIVERY REQUEST
        // =========================
        public async Task RejectDeliveryPersonAsync(int deliveryPersonId, string? reason)
        {
            var deliveryPerson = await _deliveryPersonRepository.GetByIdAsync(deliveryPersonId);

            if (deliveryPerson == null)
                throw new Exception("Delivery request not found.");

            deliveryPerson.Status = "Rejected";
            deliveryPerson.IsActive = false;
            deliveryPerson.ApprovedAt = null;
            deliveryPerson.RejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "Rejected by admin."
                : reason;

            await _deliveryPersonRepository.UpdateAsync(deliveryPerson);
        }

        // =========================
        // CHECK DELIVERY APPROVAL
        // Used by Login / ExternalLogin
        // =========================
        public async Task<bool> IsDeliveryApprovedAsync(int userId)
        {
            var deliveryPerson = await _deliveryPersonRepository.GetByUserIdAsync(userId);

            return deliveryPerson != null &&
                   deliveryPerson.Status == "Approved" &&
                   deliveryPerson.IsActive;
        }

        // =========================
        // ASSIGN DELIVERY TO ORDER
        // =========================
        public async Task<int> AssignDeliveryAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found.");

            var existingAssignment = await _assignmentRepository
                .GetActiveAssignmentByOrderAsync(orderId);

            if (existingAssignment != null)
                throw new Exception("Order already assigned.");

            var availableDeliveryPeople = await _deliveryPersonRepository.GetAvailableAsync();

            var selectedDeliveryPerson = availableDeliveryPeople
                .Where(d => d.IsActive && d.Status == "Approved")
                .OrderByDescending(d => d.Rating)
                .FirstOrDefault();

            if (selectedDeliveryPerson == null)
                throw new Exception("No available delivery person found.");

            var assignment = new DeliveryAssignment
            {
                OrderID = orderId,
                DeliveryPersonID = selectedDeliveryPerson.DeliveryPersonID,
                Status = "Assigned",
                AssignedAt = DateTime.UtcNow
            };

            var saved = await _assignmentRepository.AddAsync(assignment);

            return saved.AssignmentID;
        }

        // =========================
        // UPDATE DELIVERY STATUS
        // =========================
        public async Task UpdateDeliveryStatusAsync(
            int assignmentId,
            string status,
            string? proofUrl = null)
        {
            var assignment = await _assignmentRepository.GetByIdAsync(assignmentId);

            if (assignment == null)
                throw new Exception("Assignment not found.");

            assignment.Status = status;

            if (status == "PickedUp")
                assignment.PickupTime = DateTime.UtcNow;

            if (status == "Delivered")
                assignment.DeliveryTime = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(proofUrl))
                assignment.DeliveryProofImageURL = proofUrl;

            await _assignmentRepository.UpdateAsync(assignment);
        }
    }
}