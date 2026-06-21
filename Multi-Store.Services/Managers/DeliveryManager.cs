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

        private const string DefaultDeliveryPassword = "Delivery@12345";

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

        // =====================================================
        // REGISTER DELIVERY REQUEST
        //
        // Before approval:
        // UserID = original customer account ID
        // RequestedByUserID = original customer account ID
        // Status = Pending
        //
        // PhoneNumber is contact/work information only.
        // It is not used to identify the original customer.
        // =====================================================
        public async Task<int> RegisterDeliveryPersonAsync(
            DeliveryPersonDTO dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.UserID <= 0)
            {
                throw new InvalidOperationException("Invalid user.");
            }

            if (string.IsNullOrWhiteSpace(dto.FullName))
            {
                throw new InvalidOperationException(
                    "Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                throw new InvalidOperationException(
                    "Phone number is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Area))
            {
                throw new InvalidOperationException(
                    "Area is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.VehicleType))
            {
                throw new InvalidOperationException(
                    "Vehicle type is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.VehicleNumber))
            {
                throw new InvalidOperationException(
                    "Vehicle number is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.DrivingLicenseNumber))
            {
                throw new InvalidOperationException(
                    "Driving license number is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.IDProofURL))
            {
                throw new InvalidOperationException(
                    "ID proof is required.");
            }

            // Search by the permanent original-customer link first.
            var existing =
                await _deliveryPersonRepository
                    .GetByRequestedByUserIdAsync(dto.UserID)
                ?? await _deliveryPersonRepository
                    .GetByUserIdAsync(dto.UserID);

            if (existing != null)
            {
                if (StatusEquals(existing.Status, "Pending"))
                {
                    throw new InvalidOperationException(
                        "You already have a pending delivery request.");
                }

                if (StatusEquals(existing.Status, "Approved"))
                {
                    throw new InvalidOperationException(
                        "You are already approved as delivery staff.");
                }

                if (StatusEquals(existing.Status, "Rejected"))
                {
                    // Preserve the original customer permanently.
                    existing.RequestedByUserID =
                        existing.RequestedByUserID
                        ?? dto.RequestedByUserID
                        ?? dto.UserID;

                    // During resubmission, UserID temporarily points
                    // to the original customer again.
                    existing.UserID = dto.UserID;

                    existing.FullName = dto.FullName.Trim();
                    existing.PhoneNumber = dto.PhoneNumber.Trim();
                    existing.Area = dto.Area.Trim();
                    existing.VehicleType = dto.VehicleType.Trim();
                    existing.VehicleNumber = dto.VehicleNumber.Trim();
                    existing.DrivingLicenseNumber =
                        dto.DrivingLicenseNumber.Trim();

                    existing.IDProofURL = dto.IDProofURL.Trim();
                    existing.RejectionReason = null;
                    existing.Status = "Pending";
                    existing.IsActive = false;
                    existing.ApprovedAt = null;

                    existing.CurrentLatitude =
                        dto.CurrentLatitude;

                    existing.CurrentLongitude =
                        dto.CurrentLongitude;

                    existing.LastLocationUpdate =
                        dto.LastLocationUpdate;

                    await _deliveryPersonRepository
                        .UpdateAsync(existing);

                    return existing.DeliveryPersonID;
                }

                throw new InvalidOperationException(
                    "A delivery request already exists for this customer.");
            }

            var deliveryPerson = new DeliveryPerson
            {
                UserID = dto.UserID,

                RequestedByUserID =
                    dto.RequestedByUserID ?? dto.UserID,

                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                Area = dto.Area.Trim(),
                VehicleType = dto.VehicleType.Trim(),
                VehicleNumber = dto.VehicleNumber.Trim(),

                DrivingLicenseNumber =
                    dto.DrivingLicenseNumber.Trim(),

                IDProofURL = dto.IDProofURL.Trim(),
                RejectionReason = null,

                CurrentLatitude =
                    dto.CurrentLatitude,

                CurrentLongitude =
                    dto.CurrentLongitude,

                LastLocationUpdate =
                    dto.LastLocationUpdate,

                Status = "Pending",
                Rating = 0,
                IsActive = false,
                ApprovedAt = null
            };

            var saved =
                await _deliveryPersonRepository
                    .AddAsync(deliveryPerson);

            return saved.DeliveryPersonID;
        }

        // =====================================================
        // GET ALL DELIVERY PEOPLE / REQUESTS
        // =====================================================
        public async Task<List<DeliveryPersonDTO>> GetAllAsync()
        {
            var list =
                await _deliveryPersonRepository.GetAllAsync();

            return list
                .Select(d => new DeliveryPersonDTO
                {
                    DeliveryPersonID =
                        d.DeliveryPersonID,

                    UserID =
                        d.UserID,

                    RequestedByUserID =
                        d.RequestedByUserID,

                    FullName =
                        d.FullName,

                    PhoneNumber =
                        d.PhoneNumber,

                    Area =
                        d.Area,

                    VehicleType =
                        d.VehicleType,

                    VehicleNumber =
                        d.VehicleNumber,

                    DrivingLicenseNumber =
                        d.DrivingLicenseNumber,

                    IDProofURL =
                        d.IDProofURL,

                    RejectionReason =
                        d.RejectionReason,

                    CurrentLatitude =
                        d.CurrentLatitude,

                    CurrentLongitude =
                        d.CurrentLongitude,

                    LastLocationUpdate =
                        d.LastLocationUpdate,

                    Status =
                        d.Status,

                    Rating =
                        d.Rating,

                    IsActive =
                        d.IsActive,

                    ApprovedAt =
                        d.ApprovedAt,

                    User =
                        d.User
                })
                .ToList();
        }

        // =====================================================
        // GET PENDING DELIVERY REQUESTS
        // =====================================================
        public async Task<List<DeliveryPersonDTO>>
            GetPendingDeliveryPersonsAsync()
        {
            var list = await GetAllAsync();

            return list
                .Where(d =>
                    StatusEquals(d.Status, "Pending"))
                .OrderByDescending(d =>
                    d.DeliveryPersonID)
                .ToList();
        }

        // =====================================================
        // APPROVE DELIVERY REQUEST
        //
        // Creates a separate delivery login:
        // delivery1@gmail.com
        // Delivery@12345
        //
        // After approval:
        // RequestedByUserID = original customer account ID
        // UserID = generated delivery account ID
        // =====================================================
        public async Task<(string email, string password)>
            ApproveDeliveryPersonAsync(int deliveryPersonId)
        {
            if (deliveryPersonId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid delivery request.");
            }

            var deliveryPerson =
                await _deliveryPersonRepository
                    .GetByIdAsync(deliveryPersonId);

            if (deliveryPerson == null)
            {
                throw new InvalidOperationException(
                    "Delivery request not found.");
            }

            // Avoid creating a second account when Approve
            // is clicked again.
            if (StatusEquals(
                    deliveryPerson.Status,
                    "Approved"))
            {
                var approvedUser =
                    await _userManager.FindByIdAsync(
                        deliveryPerson.UserID.ToString());

                if (approvedUser == null)
                {
                    throw new InvalidOperationException(
                        "Approved delivery user account was not found.");
                }

                if (!await _userManager.IsInRoleAsync(
                        approvedUser,
                        "Delivery"))
                {
                    var addRoleResult =
                        await _userManager.AddToRoleAsync(
                            approvedUser,
                            "Delivery");

                    if (!addRoleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            GetIdentityErrors(addRoleResult));
                    }
                }

                if (!approvedUser.IsActive)
                {
                    approvedUser.IsActive = true;

                    var updateUserResult =
                        await _userManager.UpdateAsync(
                            approvedUser);

                    if (!updateUserResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            GetIdentityErrors(updateUserResult));
                    }
                }

                deliveryPerson.IsActive = true;
                deliveryPerson.RejectionReason = null;

                await _deliveryPersonRepository
                    .UpdateAsync(deliveryPerson);

                return (
                    approvedUser.Email
                    ?? approvedUser.UserName
                    ?? string.Empty,
                    DefaultDeliveryPassword);
            }

            if (!StatusEquals(
                    deliveryPerson.Status,
                    "Pending"))
            {
                throw new InvalidOperationException(
                    "Only pending delivery requests can be approved.");
            }

            // Save the original customer before replacing UserID.
            deliveryPerson.RequestedByUserID ??=
                deliveryPerson.UserID;

            var originalCustomer =
                await _userManager.FindByIdAsync(
                    deliveryPerson.RequestedByUserID
                        .Value
                        .ToString());

            if (originalCustomer == null)
            {
                throw new InvalidOperationException(
                    "The original customer account linked " +
                    "to this request was not found.");
            }

            string deliveryEmail;
            var counter = 1;

            do
            {
                deliveryEmail =
                    $"delivery{counter}@gmail.com";

                counter++;
            }
            while (await _userManager.FindByEmailAsync(
                       deliveryEmail) != null);

            var deliveryUser = new User
            {
                UserName = deliveryEmail,
                Email = deliveryEmail,

                FullName =
                    deliveryPerson.FullName,

                // This can be a separate delivery/work number.
                PhoneNumber =
                    deliveryPerson.PhoneNumber,

                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult =
                await _userManager.CreateAsync(
                    deliveryUser,
                    DefaultDeliveryPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    GetIdentityErrors(createResult));
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    deliveryUser,
                    "Delivery");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(
                    deliveryUser);

                throw new InvalidOperationException(
                    GetIdentityErrors(roleResult));
            }

            try
            {
                deliveryPerson.UserID =
                    deliveryUser.Id;

                deliveryPerson.Status =
                    "Approved";

                deliveryPerson.IsActive =
                    true;

                deliveryPerson.ApprovedAt =
                    DateTime.UtcNow;

                deliveryPerson.RejectionReason =
                    null;

                await _deliveryPersonRepository
                    .UpdateAsync(deliveryPerson);
            }
            catch
            {
                // Avoid leaving an unused delivery account if
                // updating the profile fails.
                await _userManager.DeleteAsync(
                    deliveryUser);

                throw;
            }

            return (
                deliveryEmail,
                DefaultDeliveryPassword);
        }

        // =====================================================
        // REJECT DELIVERY REQUEST
        // =====================================================
        public async Task RejectDeliveryPersonAsync(
            int deliveryPersonId,
            string? reason)
        {
            if (deliveryPersonId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid delivery request.");
            }

            var deliveryPerson =
                await _deliveryPersonRepository
                    .GetByIdAsync(deliveryPersonId);

            if (deliveryPerson == null)
            {
                throw new InvalidOperationException(
                    "Delivery request not found.");
            }

            if (StatusEquals(
                    deliveryPerson.Status,
                    "Approved"))
            {
                throw new InvalidOperationException(
                    "An approved delivery account cannot be rejected.");
            }

            deliveryPerson.Status = "Rejected";
            deliveryPerson.IsActive = false;
            deliveryPerson.ApprovedAt = null;

            deliveryPerson.RejectionReason =
                string.IsNullOrWhiteSpace(reason)
                    ? "Rejected by admin."
                    : reason.Trim();

            await _deliveryPersonRepository
                .UpdateAsync(deliveryPerson);
        }

        // =====================================================
        // ACTIVATE APPROVED DELIVERY PERSON
        // =====================================================
        public async Task ActivateDeliveryPersonAsync(
            int deliveryPersonId)
        {
            var deliveryPerson =
                await GetApprovedDeliveryPersonAsync(
                    deliveryPersonId);

            var user =
                await _userManager.FindByIdAsync(
                    deliveryPerson.UserID.ToString());

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Delivery user account was not found.");
            }

            if (!await _userManager.IsInRoleAsync(
                    user,
                    "Delivery"))
            {
                var roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        "Delivery");

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        GetIdentityErrors(roleResult));
                }
            }

            if (!user.IsActive)
            {
                user.IsActive = true;

                var userUpdateResult =
                    await _userManager.UpdateAsync(user);

                if (!userUpdateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        GetIdentityErrors(userUpdateResult));
                }
            }

            deliveryPerson.IsActive = true;
            deliveryPerson.RejectionReason = null;

            await _deliveryPersonRepository
                .UpdateAsync(deliveryPerson);
        }

        // =====================================================
        // DEACTIVATE APPROVED DELIVERY PERSON
        // =====================================================
        public async Task DeactivateDeliveryPersonAsync(
            int deliveryPersonId)
        {
            var deliveryPerson =
                await GetApprovedDeliveryPersonAsync(
                    deliveryPersonId);

            var user =
                await _userManager.FindByIdAsync(
                    deliveryPerson.UserID.ToString());

            // Keep the Delivery role so reactivation does not
            // modify identity structure repeatedly.
            // IsActive prevents use in assignment and dashboard.
            if (user != null && user.IsActive)
            {
                user.IsActive = false;

                var userUpdateResult =
                    await _userManager.UpdateAsync(user);

                if (!userUpdateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        GetIdentityErrors(userUpdateResult));
                }
            }

            deliveryPerson.IsActive = false;

            await _deliveryPersonRepository
                .UpdateAsync(deliveryPerson);
        }

        // =====================================================
        // CHECK DELIVERY APPROVAL
        //
        // userId is the generated Delivery account ID.
        // =====================================================
        public async Task<bool> IsDeliveryApprovedAsync(
            int userId)
        {
            if (userId <= 0)
            {
                return false;
            }

            var deliveryPerson =
                await _deliveryPersonRepository
                    .GetByUserIdAsync(userId);

            return deliveryPerson != null &&
                   StatusEquals(
                       deliveryPerson.Status,
                       "Approved") &&
                   deliveryPerson.IsActive;
        }

        // =====================================================
        // AUTO-ASSIGN DELIVERY TO ORDER
        //
        // This method prevents self-delivery.
        //
        // The main AdminAssignDelivery page is responsible
        // for manual assignment, order status, and notification.
        // =====================================================
        public async Task<int> AssignDeliveryAsync(int orderId)
        {
            if (orderId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid order.");
            }

            var order =
                await _orderRepository
                    .GetOrderDetailsAsync(orderId);

            if (order == null)
            {
                throw new InvalidOperationException(
                    "Order not found.");
            }

            if (order.Customer == null)
            {
                throw new InvalidOperationException(
                    "Customer linked to this order was not found.");
            }

            if (StatusEquals(order.Status, "Delivered") ||
                StatusEquals(order.Status, "Cancelled"))
            {
                throw new InvalidOperationException(
                    "Delivered or cancelled orders cannot be assigned.");
            }

            var existingAssignment =
                await _assignmentRepository
                    .GetActiveAssignmentByOrderAsync(orderId);

            if (existingAssignment != null)
            {
                throw new InvalidOperationException(
                    "Order already has an active delivery assignment.");
            }

            var availableDeliveryPeople =
                await _deliveryPersonRepository
                    .GetAvailableAsync();

            var selectedDeliveryPerson =
                availableDeliveryPeople
                    .Where(d =>
                        d.IsActive &&
                        StatusEquals(
                            d.Status,
                            "Approved") &&
                        !IsOwnCustomerOrder(
                            d,
                            order.Customer))
                    .OrderByDescending(d =>
                        d.Rating)
                    .ThenBy(d =>
                        d.DeliveryPersonID)
                    .FirstOrDefault();

            if (selectedDeliveryPerson == null)
            {
                throw new InvalidOperationException(
                    "No eligible delivery person was found. " +
                    "A customer cannot deliver their own order.");
            }

            var assignment = new DeliveryAssignment
            {
                OrderID = orderId,

                DeliveryPersonID =
                    selectedDeliveryPerson
                        .DeliveryPersonID,

                Status = "Assigned",
                AssignedAt = DateTime.UtcNow,
                PickupTime = null,
                DeliveryTime = null,
                DeliveryProofImageURL = null
            };

            var saved =
                await _assignmentRepository
                    .AddAsync(assignment);

            return saved.AssignmentID;
        }

        // =====================================================
        // UPDATE DELIVERY STATUS
        // =====================================================
        public async Task UpdateDeliveryStatusAsync(
            int assignmentId,
            string status,
            string? proofUrl = null)
        {
            if (assignmentId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid assignment.");
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new InvalidOperationException(
                    "Delivery status is required.");
            }

            var assignment =
                await _assignmentRepository
                    .GetByIdAsync(assignmentId);

            if (assignment == null)
            {
                throw new InvalidOperationException(
                    "Assignment not found.");
            }

            var normalizedStatus = status.Trim();

            assignment.Status =
                normalizedStatus;

            if (StatusEquals(
                    normalizedStatus,
                    "PickedUp") ||
                StatusEquals(
                    normalizedStatus,
                    "OutForDelivery") ||
                StatusEquals(
                    normalizedStatus,
                    "Out for Delivery"))
            {
                assignment.PickupTime ??=
                    DateTime.UtcNow;
            }

            if (StatusEquals(
                    normalizedStatus,
                    "Delivered"))
            {
                assignment.DeliveryTime =
                    DateTime.UtcNow;
            }

            if (!string.IsNullOrWhiteSpace(proofUrl))
            {
                assignment.DeliveryProofImageURL =
                    proofUrl.Trim();
            }

            await _assignmentRepository
                .UpdateAsync(assignment);
        }

        // =====================================================
        // HELPERS
        // =====================================================
        private async Task<DeliveryPerson>
            GetApprovedDeliveryPersonAsync(
                int deliveryPersonId)
        {
            if (deliveryPersonId <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid delivery person.");
            }

            var deliveryPerson =
                await _deliveryPersonRepository
                    .GetByIdAsync(deliveryPersonId);

            if (deliveryPerson == null)
            {
                throw new InvalidOperationException(
                    "Delivery person not found.");
            }

            if (!StatusEquals(
                    deliveryPerson.Status,
                    "Approved"))
            {
                throw new InvalidOperationException(
                    "Only approved delivery staff can be modified.");
            }

            return deliveryPerson;
        }

        private static bool IsOwnCustomerOrder(
            DeliveryPerson deliveryPerson,
            Customer customer)
        {
            // Profiles without RequestedByUserID are considered
            // unsafe and cannot be automatically assigned.
            if (!deliveryPerson.RequestedByUserID.HasValue)
            {
                return true;
            }

            return deliveryPerson.RequestedByUserID.Value ==
                   customer.UserID;
        }

        private static bool StatusEquals(
            string? currentStatus,
            string expectedStatus)
        {
            return string.Equals(
                currentStatus?.Trim(),
                expectedStatus,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetIdentityErrors(
            IdentityResult result)
        {
            return string.Join(
                " | ",
                result.Errors.Select(
                    e => e.Description));
        }
    }
}

