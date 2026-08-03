using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using Multi_Store.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Multi_Store.Services.Managers
{
    public class DeliveryReviewManager
    {
        private readonly IDeliveryReviewRepository _deliveryReviewRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ApplicationDbContext _context; // order/delivery-person lookups + rating cache write + notification

        public DeliveryReviewManager(
            IDeliveryReviewRepository deliveryReviewRepository,
            IAuditLogRepository auditLogRepository,
            ApplicationDbContext context)
        {
            _deliveryReviewRepository = deliveryReviewRepository;
            _auditLogRepository = auditLogRepository;
            _context = context;
        }

        // Used by the Customer Orders page to decide between the
        // "Review Delivery" button and the "Reviewed" badge, without
        // needing to know anything else about this domain.
        public async Task<bool> IsOrderReviewedAsync(int orderId)
        {
            return await _deliveryReviewRepository.ExistsForOrderAsync(orderId);
        }

        // Used by the public Delivery Profile page — read-only, no
        // validation needed, same purpose as ReviewManager.GetReviewsByStoreAsync.
        public async Task<IEnumerable<DeliveryReviewDTO>> GetReviewsByDeliveryPersonAsync(int deliveryPersonId)
        {
            var reviews = await _deliveryReviewRepository.GetByDeliveryPersonAsync(deliveryPersonId);

            return reviews.Select(r => new DeliveryReviewDTO
            {
                DeliveryReviewID = r.DeliveryReviewID,
                OrderID = r.OrderID,
                CustomerID = r.CustomerID,
                CustomerName = r.Customer?.User?.FullName,
                DeliveryPersonID = r.DeliveryPersonID,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            });
        }

        public async Task<DeliveryReviewDTO> AddReviewAsync(
            int orderId,
            int customerId,
            int rating,
            string? comment,
            string ipAddress,
            string userAgent)
        {
            if (customerId <= 0)
                throw new Exception("Invalid customer.");

            if (orderId <= 0)
                throw new Exception("Invalid order.");

            if (rating < 1 || rating > 5)
                throw new Exception("Rating must be between 1 and 5.");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
                throw new Exception("Order not found.");

            // Ownership check — only the customer who placed the order
            // may review its delivery.
            if (order.CustomerID != customerId)
                throw new Exception("You are not authorized to review this order.");

            // Only delivered orders can be reviewed.
            if (!string.Equals(order.Status?.Trim(), "Delivered", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Only delivered orders can be reviewed.");

            // One review per order.
            var alreadyReviewed = await _deliveryReviewRepository.ExistsForOrderAsync(orderId);
            if (alreadyReviewed)
                throw new Exception("This delivery has already been reviewed.");

            // Resolve the delivery person server-side from the order's
            // own assignment history — never trust a delivery-person id
            // supplied by the client, since that would let a customer
            // rate a driver who never handled their order.
            var assignment = await _context.DeliveryAssignments
                .Where(a => a.OrderID == orderId &&
                            a.Status != "Cancelled" &&
                            a.Status != "Failed")
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            if (assignment == null)
                throw new Exception("No delivery assignment found for this order.");

            var deliveryPersonId = assignment.DeliveryPersonID;

            var review = new DeliveryReview
            {
                OrderID = orderId,
                CustomerID = customerId,
                DeliveryPersonID = deliveryPersonId,
                Rating = rating,
                Comment = comment?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _deliveryReviewRepository.AddAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = customerId,
                Action = "AddDeliveryReview",
                EntityName = "DeliveryReview",
                EntityID = review.DeliveryReviewID.ToString(),
                OldValue = null,
                NewValue = $"Delivery review added with rating {review.Rating}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            var deliveryPerson = await RecalculateRatingAsync(deliveryPersonId);

            // Notify the delivery partner, same pattern as ReviewManager
            // notifying a store owner about a new product/store review.
            _context.Notifications.Add(new Notification
            {
                UserID = deliveryPerson.UserID,
                Title = "New delivery review",
                Message = $"A customer left a {review.Rating}-star review on a delivery you completed.",
                Type = "DeliveryReview",
                ReferenceID = review.DeliveryReviewID,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                SentVia = "System"
            });

            await _context.SaveChangesAsync();

            return new DeliveryReviewDTO
            {
                DeliveryReviewID = review.DeliveryReviewID,
                OrderID = review.OrderID,
                CustomerID = review.CustomerID,
                DeliveryPersonID = review.DeliveryPersonID,
                DeliveryPersonName = deliveryPerson.FullName,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt
            };
        }

        // Recalculates DeliveryPerson.Rating as the average of every
        // DeliveryReview for that delivery person, and persists it back
        // onto the cached field — DeliveryReview is the source of truth,
        // DeliveryPerson.Rating is just a denormalized cache of it.
        private async Task<DeliveryPerson> RecalculateRatingAsync(int deliveryPersonId)
        {
            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d => d.DeliveryPersonID == deliveryPersonId);

            if (deliveryPerson == null)
                throw new Exception("Delivery person not found.");

            var reviews = await _deliveryReviewRepository.GetByDeliveryPersonAsync(deliveryPersonId);

            deliveryPerson.Rating = reviews.Count > 0
                ? (decimal)reviews.Average(r => r.Rating)
                : 0m;

            return deliveryPerson;
        }
    }
}
