using Microsoft.Extensions.Logging;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Repositories;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class DeliveryManager
    {
        private readonly IDeliveryPersonRepository _deliveryRepo;
        private readonly IDeliveryAssignmentRepository _assignmentRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly ILogger<DeliveryManager> _logger;

        public DeliveryManager(
            IDeliveryPersonRepository deliveryRepo,
            IDeliveryAssignmentRepository assignmentRepo,
            IOrderRepository orderRepo,
             ILogger<DeliveryManager> logger)
        {
            _deliveryRepo = deliveryRepo;
            _assignmentRepo = assignmentRepo;
            _orderRepo = orderRepo;
            _logger = logger;
        }

        // =========================
        // REGISTER DELIVERY (CUSTOMER REQUEST)
        // =========================
        public async Task<int> RegisterDeliveryPersonAsync(DeliveryPersonDTO dto)
        {
            _logger.LogInformation("REGISTER DELIVERY STARTED");

            var entity = new DeliveryPerson
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                VehicleType = dto.VehicleType,
                VehicleNumber = dto.VehicleNumber,
                Area = dto.Area,
                DrivingLicenseNumber = dto.DrivingLicenseNumber,
                Status = "Pending",
                IsActive = false,
                UserID = dto.UserID
            };

            await _deliveryRepo.AddAsync(entity);

            _logger.LogInformation(
                "DELIVERY SAVED. ID={Id} Name={Name} Status={Status}",
                entity.DeliveryPersonID,
                entity.FullName,
                entity.Status);

            return entity.DeliveryPersonID;
        }
        public async Task<List<DeliveryPerson>> GetPendingDeliveryPersonsAsync()
        {
            var list = await _deliveryRepo.GetAllAsync();

            return list
                .Where(x => x.Status != null &&
                            x.Status.Trim().Equals("Pending", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // =========================
        // APPROVE DELIVERY
        public async Task ApproveDeliveryPersonAsync(int id)
        {
            var delivery = await _deliveryRepo.GetByIdAsync(id);

            if (delivery == null)
                throw new Exception("Delivery person not found");

            delivery.Status = "Approved";
            delivery.IsActive = true;
            delivery.ApprovedAt = DateTime.UtcNow;

            await _deliveryRepo.UpdateAsync(delivery);
        }
        // =========================
        // REJECT DELIVERY
        // =========================
        public async Task RejectDeliveryPersonAsync(int id)
        {
            var delivery = await _deliveryRepo.GetByIdAsync(id);

            if (delivery == null)
                throw new Exception("Delivery person not found");

            delivery.Status = "Rejected";
            delivery.IsActive = false;

            await _deliveryRepo.UpdateAsync(delivery);
        }

        // =========================
        // CHECK APPROVAL (LOGIN FLOW)
        // =========================
        public async Task<bool> IsDeliveryApprovedAsync(int userId)
        {
            var delivery = await _deliveryRepo.GetByUserIdAsync(userId);

            return delivery != null &&
                   delivery.Status == "Approved" &&
                   delivery.IsActive;
        }

        // =========================
        // ASSIGN DELIVERY TO ORDER
        // =========================
        public async Task<int> AssignDeliveryAsync(int orderId)
        {
            var order = await _orderRepo.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            var existing = await _assignmentRepo.GetActiveAssignmentByOrderAsync(orderId);

            if (existing != null)
                throw new Exception("Order already assigned");

            var available = await _deliveryRepo.GetAvailableAsync();

            if (!available.Any())
                throw new Exception("No delivery available");

            var selected = available
                .OrderBy(x => x.Assignments.Count)
                .First();

            var assignment = new DeliveryAssignment
            {
                OrderID = orderId,
                DeliveryPersonID = selected.DeliveryPersonID,
                Status = "Assigned",
                AssignedAt = DateTime.UtcNow
            };

            var saved = await _assignmentRepo.AddAsync(assignment);

            return saved.AssignmentID;
        }

        // =========================
        // UPDATE DELIVERY STATUS
        // =========================
        public async Task UpdateDeliveryStatusAsync(int assignmentId, string status, string? proofUrl = null)
        {
            var assignment = await _assignmentRepo.GetByIdAsync(assignmentId);

            if (assignment == null)
                throw new Exception("Assignment not found");

            assignment.Status = status;

            if (status == "PickedUp")
                assignment.PickupTime = DateTime.UtcNow;

            if (status == "Delivered")
                assignment.DeliveryTime = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(proofUrl))
                assignment.DeliveryProofImageURL = proofUrl;

            await _assignmentRepo.UpdateAsync(assignment);

            if (status == "Delivered")
            {
                var delivery = await _deliveryRepo.GetByIdAsync(assignment.DeliveryPersonID);

                if (delivery != null)
                {
                    delivery.Status = "Available";
                    await _deliveryRepo.UpdateAsync(delivery);
                }
            }
        }

        // =========================
        // GET ALL (ADMIN DEBUG)
        // =========================
        public async Task<IReadOnlyList<DeliveryPerson>> GetAllAsync()
        {
            return await _deliveryRepo.GetAllAsync();
        }
    }
}