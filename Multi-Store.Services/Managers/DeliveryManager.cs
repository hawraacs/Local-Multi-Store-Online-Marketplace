using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class DeliveryManager
    {
        private readonly IDeliveryPersonRepository _deliveryPersonRepository;
        private readonly IDeliveryAssignmentRepository _assignmentRepository;
        private readonly IOrderRepository _orderRepository;

        public DeliveryManager(
            IDeliveryPersonRepository deliveryPersonRepository,
            IDeliveryAssignmentRepository assignmentRepository,
            IOrderRepository orderRepository)
        {
            _deliveryPersonRepository = deliveryPersonRepository;
            _assignmentRepository = assignmentRepository;
            _orderRepository = orderRepository;
        }

        // =====================================================
        // ASSIGN DELIVERY PERSON TO ORDER
        // =====================================================
        public async Task<int> AssignDeliveryAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            // prevent duplicate assignment
            var existing = await _assignmentRepository.GetActiveAssignmentByOrderAsync(orderId);

            if (existing != null)
                throw new Exception("Order already assigned");

            // get available delivery persons
            var deliveryPersons = await _deliveryPersonRepository.GetAvailableAsync();

            if (!deliveryPersons.Any())
                throw new Exception("No delivery person available");

            // simple assignment logic (lowest workload = first available)
            var selected = deliveryPersons
                .OrderBy(d => d.Assignments.Count)
                .First();

            var assignment = new DeliveryAssignment
            {
                OrderID = orderId,
                DeliveryPersonID = selected.DeliveryPersonID,
                AssignedAt = DateTime.UtcNow,
                Status = "Assigned"
            };

            await _assignmentRepository.AddAsync(assignment);

            return assignment.AssignmentID;
        }

        // =====================================================
        // UPDATE DELIVERY STATUS
        // =====================================================
        public async Task UpdateDeliveryStatusAsync(
            int assignmentId,
            string status,
            string? proofImageUrl = null)
        {
            var assignment = await _assignmentRepository.GetByIdAsync(assignmentId);

            if (assignment == null)
                throw new Exception("Assignment not found");

            assignment.Status = status;

            if (status == "PickedUp")
                assignment.PickupTime = DateTime.UtcNow;

            if (status == "Delivered")
                assignment.DeliveryTime = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(proofImageUrl))
                assignment.DeliveryProofImageURL = proofImageUrl;

            await _assignmentRepository.UpdateAsync(assignment);

            // update delivery person status if needed
            if (status == "Delivered")
            {
                var deliveryPerson = await _deliveryPersonRepository.GetByIdAsync(assignment.DeliveryPersonID);

                if (deliveryPerson != null)
                {
                    deliveryPerson.Status = "Available";
                    await _deliveryPersonRepository.UpdateAsync(deliveryPerson);
                }
            }
        }

        // =====================================================
        // GET DELIVERY TRACKING INFO
        // =====================================================
        public async Task<DeliveryAssignment> GetAssignmentDetailsAsync(int orderId)
        {
            var assignment = await _assignmentRepository.GetActiveAssignmentByOrderAsync(orderId);

            if (assignment == null)
                throw new Exception("No active delivery assignment found");

            return assignment;
        }

        // =====================================================
        // GET DELIVERY PERSON ASSIGNMENTS
        // =====================================================
        public async Task<IReadOnlyList<DeliveryAssignment>> GetDeliveryPersonTasksAsync(int deliveryPersonId)
        {
            return await _assignmentRepository.GetByDeliveryPersonAsync(deliveryPersonId);
        }

        // =====================================================
        // MARK PICKUP
        // =====================================================
        public async Task MarkPickedUpAsync(int assignmentId)
        {
            var assignment = await _assignmentRepository.GetByIdAsync(assignmentId);

            if (assignment == null)
                throw new Exception("Assignment not found");

            assignment.Status = "PickedUp";
            assignment.PickupTime = DateTime.UtcNow;

            await _assignmentRepository.UpdateAsync(assignment);
        }

        // =====================================================
        // MARK DELIVERED
        // =====================================================
        public async Task MarkDeliveredAsync(int assignmentId, string? proofUrl = null)
        {
            var assignment = await _assignmentRepository.GetByIdAsync(assignmentId);

            if (assignment == null)
                throw new Exception("Assignment not found");

            assignment.Status = "Delivered";
            assignment.DeliveryTime = DateTime.UtcNow;
            assignment.DeliveryProofImageURL = proofUrl;

            await _assignmentRepository.UpdateAsync(assignment);

            // free delivery person
            var deliveryPerson = await _deliveryPersonRepository.GetByIdAsync(assignment.DeliveryPersonID);

            if (deliveryPerson != null)
            {
                deliveryPerson.Status = "Available";
                await _deliveryPersonRepository.UpdateAsync(deliveryPerson);
            }
        }
        public async Task<bool> IsDeliveryApprovedAsync(int userId)
        {
            var delivery = await _deliveryPersonRepository.GetByUserIdAsync(userId);

            return delivery != null && delivery.Status != "Pending" && delivery.Status != "Rejected";
        }
    }
}
