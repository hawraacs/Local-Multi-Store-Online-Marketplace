using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    // =====================================================
    // DELIVERY REVIEW MANAGER
    //
    // Deliberately kept separate from DeliveryManager, which already
    // owns delivery person registration, approval, assignment, and
    // delivery workflow logic. This manager mirrors the internal
    // structure of ReviewManager (the only full Manager-layer
    // precedent we have for reviews): validation, an AuditLog entry,
    // and a Notification are all written from inside AddReviewAsync.
    // =====================================================
    public class DeliveryReviewManager : IDeliveryReviewManager
    {
        private readonly IDeliveryReviewRepository _deliveryReviewRepository;
        private readonly IDeliveryAssignmentRepository _assignmentRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IDeliveryPersonRepository _deliveryPersonRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ApplicationDbContext _context; // notification write only, same as ReviewManager

        public DeliveryReviewManager(
            IDeliveryReviewRepository deliveryReviewRepository,
            IDeliveryAssignmentRepository assignmentRepository,
            IOrderRepository orderRepository,
            IDeliveryPersonRepository deliveryPersonRepository,
            IAuditLogRepository auditLogRepository,
            ApplicationDbContext context)
        {
            _deliveryReviewRepository = deliveryReviewRepository;
            _assignmentRepository = assignmentRepository;
            _orderRepository = orderRepository;
            _deliveryPersonRepository = deliveryPersonRepository;
            _auditLogRepository = auditLogRepository;
            _context = context;
        }

        // =====================================================
        // ADD REVIEW
        //
        // Validation order:
        // 1. Basic field validation (ids present, rating in range).
        // 2. The assignment exists and belongs to the given delivery person.
        // 3. The assignment is actually completed ("Delivered").
        // 4. The assignment's order belongs to the reviewing customer.
        // 5. No review already exists for this assignment.
        //
        // Navigation properties are never assumed to be loaded - Order
        // is fetched separately via IOrderRepository, the same pattern
        // DeliveryManager.ConfirmCashCollectionAsync already uses
        // (GetOrderDetailsAsync(assignment.OrderID)).
        // =====================================================
        public async Task<int> AddReviewAsync(
            DeliveryReviewDTO reviewDTO,
            string ipAddress,
            string userAgent)
        {
            if (reviewDTO == null)
            {
                throw new ArgumentNullException(nameof(reviewDTO));
            }

            if (reviewDTO.CustomerID <= 0)
            {
                throw new InvalidOperationException("Invalid customer.");
            }

            if (reviewDTO.DeliveryPersonID <= 0)
            {
                throw new InvalidOperationException("Invalid delivery person.");
            }

            if (reviewDTO.AssignmentID <= 0)
            {
                throw new InvalidOperationException("Invalid delivery assignment.");
            }

            if (reviewDTO.Rating < 1 || reviewDTO.Rating > 5)
            {
                throw new InvalidOperationException("Rating must be between 1 and 5.");
            }

            // Base Repository<T>.GetByIdAsync uses FindAsync, which does
            // NOT populate navigation properties - Order/DeliveryPerson
            // on the returned assignment cannot be assumed to be loaded.
            var assignment = await _assignmentRepository.GetByIdAsync(reviewDTO.AssignmentID);

            if (assignment == null)
            {
                throw new InvalidOperationException("Delivery assignment not found.");
            }

            if (assignment.DeliveryPersonID != reviewDTO.DeliveryPersonID)
            {
                throw new InvalidOperationException(
                    "This assignment does not belong to the specified delivery person.");
            }

            if (!string.Equals(
                    assignment.Status?.Trim(),
                    "Delivered",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Reviews are only allowed after a completed delivery.");
            }

            // Fetch the order separately - same call DeliveryManager already
            // makes from an assignment (GetOrderDetailsAsync(assignment.OrderID)) -
            // to confirm the reviewing customer actually received this delivery.
            var order = await _orderRepository.GetOrderDetailsAsync(assignment.OrderID);

            if (order == null)
            {
                throw new InvalidOperationException("Order for this assignment was not found.");
            }

            if (order.CustomerID != reviewDTO.CustomerID)
            {
                throw new InvalidOperationException(
                    "You can only review deliveries from your own orders.");
            }

            var alreadyReviewed = await _deliveryReviewRepository
                .ExistsForAssignmentAsync(reviewDTO.AssignmentID);

            if (alreadyReviewed)
            {
                throw new InvalidOperationException(
                    "This delivery has already been reviewed.");
            }

            var review = new DeliveryReview
            {
                CustomerID = reviewDTO.CustomerID,
                DeliveryPersonID = reviewDTO.DeliveryPersonID,
                AssignmentID = reviewDTO.AssignmentID,
                Rating = reviewDTO.Rating,
                Comment = reviewDTO.Comment,

                // Matches the live, working Store review paths (auto-visible,
                // no moderation queue) rather than ReviewManager's unused
                // "Pending" default, since no admin moderation workflow for
                // delivery reviews exists or has been requested.
                Status = "Approved",

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            await _deliveryReviewRepository.AddAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = review.CustomerID,
                Action = "AddDeliveryReview",
                EntityName = "DeliveryReview",
                EntityID = review.DeliveryReviewID.ToString(),
                OldValue = null,
                NewValue = $"Delivery review added with rating {review.Rating}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            // Recalculate the cached DeliveryPerson.Rating from all
            // non-rejected reviews, then notify the delivery person -
            // both reuse the same DeliveryPerson lookup.
            var deliveryPerson = await _deliveryPersonRepository
                .GetByIdAsync(reviewDTO.DeliveryPersonID);

            if (deliveryPerson != null)
            {
                var allReviews = await _deliveryReviewRepository
                    .GetByDeliveryPersonAsync(reviewDTO.DeliveryPersonID);

                var ratedReviews = allReviews
                    .Where(r => r.Status != "Rejected")
                    .ToList();

                deliveryPerson.Rating = ratedReviews.Any()
                    ? Math.Round((decimal)ratedReviews.Average(r => r.Rating), 2)
                    : 0;

                await _deliveryPersonRepository.UpdateAsync(deliveryPerson);

                _context.Notifications.Add(new Notification
                {
                    UserID = deliveryPerson.UserID,
                    Title = "New delivery review",
                    Message = $"A customer left a {review.Rating}-star review on one of your deliveries.",
                    Type = "DeliveryReview",
                    ReferenceID = review.DeliveryReviewID,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });

                await _context.SaveChangesAsync();
            }

            return review.DeliveryReviewID;
        }

        // =====================================================
        // LISTING / STATS
        // =====================================================
        public async Task<IEnumerable<DeliveryReviewDTO>> GetReviewsByDeliveryPersonAsync(
            int deliveryPersonId)
        {
            var reviews = await _deliveryReviewRepository
                .GetByDeliveryPersonAsync(deliveryPersonId);

            return MapToDtos(reviews);
        }

        public async Task<IEnumerable<DeliveryReviewDTO>> GetReviewsByCustomerAsync(
            int customerId)
        {
            var reviews = await _deliveryReviewRepository
                .GetByCustomerAsync(customerId);

            return MapToDtos(reviews);
        }

        public async Task<bool> ExistsForAssignmentAsync(int assignmentId)
        {
            return await _deliveryReviewRepository
                .ExistsForAssignmentAsync(assignmentId);
        }

        public async Task<double> GetAverageDeliveryPersonRatingAsync(int deliveryPersonId)
        {
            var reviews = await _deliveryReviewRepository
                .GetByDeliveryPersonAsync(deliveryPersonId);

            var approvedReviews = reviews
                .Where(r => r.Status == "Approved")
                .ToList();

            if (!approvedReviews.Any())
            {
                return 0;
            }

            return approvedReviews.Average(r => r.Rating);
        }

        public async Task<int> GetTotalReviewsCountAsync(int deliveryPersonId)
        {
            var reviews = await _deliveryReviewRepository
                .GetByDeliveryPersonAsync(deliveryPersonId);

            return reviews.Count(r => r.Status == "Approved");
        }

        // =====================================================
        // HELPERS
        // =====================================================
        private static List<DeliveryReviewDTO> MapToDtos(
            IEnumerable<DeliveryReview> reviews)
        {
            return reviews
                .Select(r => new DeliveryReviewDTO
                {
                    DeliveryReviewID = r.DeliveryReviewID,
                    CustomerID = r.CustomerID,
                    CustomerName = r.Customer?.User?.FullName,
                    DeliveryPersonID = r.DeliveryPersonID,
                    AssignmentID = r.AssignmentID,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToList();
        }
    }
}