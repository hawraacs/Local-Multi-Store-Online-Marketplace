using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using Multi_Store.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class ReviewManager
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context; // NEW — store owner lookup + notification write

        public ReviewManager(
            IReviewRepository reviewRepository,
            IAuditLogRepository auditLogRepository,
            IMapper mapper,
            ApplicationDbContext context)
        {
            _reviewRepository = reviewRepository;
            _auditLogRepository = auditLogRepository;
            _mapper = mapper;
            _context = context;
        }

        public async Task<IEnumerable<ReviewDTO>> GetAllReviewsAsync()
        {
            var reviews = await _reviewRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ReviewDTO>>(reviews);
        }

        public async Task<ReviewDTO?> GetReviewByIdAsync(int reviewId)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);
            return _mapper.Map<ReviewDTO?>(review);
        }

        public async Task<IEnumerable<ReviewDTO>> GetReviewsByProductAsync(int productId)
        {
            var reviews = await _reviewRepository.GetByProductAsync(productId);
            return _mapper.Map<IEnumerable<ReviewDTO>>(reviews);
        }

        public async Task<IEnumerable<ReviewDTO>> GetReviewsByStoreAsync(int storeId)
        {
            var reviews = await _reviewRepository.GetByStoreAsync(storeId);

            Console.WriteLine($"STORE ID = {storeId}");
            Console.WriteLine($"REPOSITORY COUNT = {reviews.Count}");

            return _mapper.Map<IEnumerable<ReviewDTO>>(reviews);
        }

        public async Task<IEnumerable<ReviewDTO>> GetReviewsByCustomerAsync(int customerId)
        {
            var reviews = await _reviewRepository.GetByCustomerAsync(customerId);
            return _mapper.Map<IEnumerable<ReviewDTO>>(reviews);
        }

        public async Task<IEnumerable<ReviewDTO>> GetReviewsByStatusAsync(string status)
        {
            var reviews = await _reviewRepository.GetByStatusAsync(status);
            return _mapper.Map<IEnumerable<ReviewDTO>>(reviews);
        }

        public async Task<int> AddReviewAsync(
            ReviewDTO reviewDTO,
            string ipAddress,
            string userAgent)
        {
            if (reviewDTO.CustomerID <= 0)
                throw new Exception("Invalid customer.");

            if (reviewDTO.ProductID != null &&
      (!reviewDTO.OrderItemID.HasValue ||
       reviewDTO.OrderItemID <= 0))
            {
                throw new Exception("Invalid order item.");
            }

            if (reviewDTO.StoreID <= 0)
                throw new Exception("Invalid store.");

            if (reviewDTO.Rating < 1 || reviewDTO.Rating > 5)
                throw new Exception("Rating must be between 1 and 5.");

            if (reviewDTO.OrderItemID.HasValue)
            {
                var exists = await _reviewRepository
                    .ExistsForOrderItemAsync(reviewDTO.OrderItemID.Value);

                if (exists)
                    throw new Exception(
                        "Review already exists for this order item.");
            }

            var review = _mapper.Map<Review>(reviewDTO);

            review.CreatedAt = DateTime.UtcNow;
            review.UpdatedAt = null;
            review.Status = "Pending";
            review.IsVerifiedPurchase = true;

            await _reviewRepository.AddAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = review.CustomerID,
                Action = "AddReview",
                EntityName = "Review",
                EntityID = review.ReviewID.ToString(),
                OldValue = null,
                NewValue = $"Review added with rating {review.Rating}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });

            // NEW — notify the store owner about the new review.
            var store = await _context.Stores.FindAsync(review.StoreID);
            if (store != null)
            {
                var isProductReview = review.ProductID.HasValue;

                _context.Notifications.Add(new Notification
                {
                    UserID = store.OwnerUserID,
                    Title = isProductReview ? "New product review" : "New store review",
                    Message = isProductReview
                        ? $"A customer left a {review.Rating}-star review on one of your products."
                        : $"A customer left a {review.Rating}-star review on your store.",
                    Type = isProductReview ? "ProductReview" : "StoreReview",
                    ReferenceID = review.ReviewID,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    SentVia = "System"
                });

                await _context.SaveChangesAsync();
            }

            return review.ReviewID;
        }

        public async Task UpdateReviewAsync(
            ReviewDTO reviewDTO,
            string ipAddress,
            string userAgent)
        {
            var existingReview =
                await _reviewRepository.GetByIdAsync(reviewDTO.ReviewID);

            if (existingReview == null)
                throw new Exception("Review not found.");

            if (reviewDTO.Rating < 1 || reviewDTO.Rating > 5)
                throw new Exception("Rating must be between 1 and 5.");

            var oldValue =
                $"Rating: {existingReview.Rating}, Comment: {existingReview.Comment}";

            existingReview.Rating = reviewDTO.Rating;
            existingReview.Comment = reviewDTO.Comment;
            existingReview.Status = reviewDTO.Status;
            existingReview.UpdatedAt = DateTime.UtcNow;

            await _reviewRepository.UpdateAsync(existingReview);

            var newValue =
                $"Rating: {existingReview.Rating}, Comment: {existingReview.Comment}";

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = existingReview.CustomerID,
                Action = "UpdateReview",
                EntityName = "Review",
                EntityID = existingReview.ReviewID.ToString(),
                OldValue = oldValue,
                NewValue = newValue,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        public async Task DeleteReviewAsync(
            int reviewId,
            string ipAddress,
            string userAgent)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);

            if (review == null)
                throw new Exception("Review not found.");

            await _reviewRepository.DeleteAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = review.CustomerID,
                Action = "DeleteReview",
                EntityName = "Review",
                EntityID = review.ReviewID.ToString(),
                OldValue = $"Rating: {review.Rating}, Comment: {review.Comment}",
                NewValue = null,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        public async Task ApproveReviewAsync(
            int reviewId,
            string ipAddress,
            string userAgent)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);

            if (review == null)
                throw new Exception("Review not found.");

            var oldValue = review.Status;

            review.Status = "Approved";
            review.UpdatedAt = DateTime.UtcNow;

            await _reviewRepository.UpdateAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = review.CustomerID,
                Action = "ApproveReview",
                EntityName = "Review",
                EntityID = review.ReviewID.ToString(),
                OldValue = oldValue,
                NewValue = "Approved",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        public async Task RejectReviewAsync(
            int reviewId,
            string reason,
            string ipAddress,
            string userAgent)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);

            if (review == null)
                throw new Exception("Review not found.");

            var oldValue = review.Status;

            review.Status = "Rejected";
            review.UpdatedAt = DateTime.UtcNow;

            await _reviewRepository.UpdateAsync(review);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserID = review.CustomerID,
                Action = "RejectReview",
                EntityName = "Review",
                EntityID = review.ReviewID.ToString(),
                OldValue = oldValue,
                NewValue = $"Rejected - Reason: {reason}",
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ActionDate = DateTime.UtcNow
            });
        }

        public async Task<bool> ReviewExistsAsync(int reviewId)
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);
            return review != null;
        }

        public async Task<bool> IsOrderItemReviewedAsync(int orderItemId)
        {
            return await _reviewRepository
                .ExistsForOrderItemAsync(orderItemId);
        }

        public async Task<int> GetTotalReviewsCountAsync()
        {
            var reviews = await _reviewRepository.GetAllAsync();
            return reviews.Count();
        }

        public async Task<int> GetApprovedReviewsCountAsync()
        {
            var reviews = await _reviewRepository
                .GetByStatusAsync("Approved");

            return reviews.Count;
        }

        public async Task<int> GetPendingReviewsCountAsync()
        {
            var reviews = await _reviewRepository
                .GetByStatusAsync("Pending");

            return reviews.Count;
        }

        public async Task<int> GetRejectedReviewsCountAsync()
        {
            var reviews = await _reviewRepository
                .GetByStatusAsync("Rejected");

            return reviews.Count;
        }

        public async Task<double> GetAverageProductRatingAsync(int productId)
        {
            var reviews = await _reviewRepository
                .GetByProductAsync(productId);

            var approvedReviews = reviews
                .Where(r => r.Status == "Approved")
                .ToList();

            if (!approvedReviews.Any())
                return 0;

            return approvedReviews.Average(r => r.Rating);
        }

        public async Task<double> GetAverageStoreRatingAsync(int storeId)
        {
            var reviews = await _reviewRepository
                .GetByStoreAsync(storeId);

            var approvedReviews = reviews
                .Where(r => r.Status == "Approved")
                .ToList();

            if (!approvedReviews.Any())
                return 0;

            return approvedReviews.Average(r => r.Rating);
        }

        public async Task<IEnumerable<ReviewDTO>> SearchReviewsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllReviewsAsync();

            var reviews = await _reviewRepository.GetAllAsync();

            var filteredReviews = reviews
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.Comment) &&
                    r.Comment.Contains(
                        searchTerm,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            return _mapper.Map<IEnumerable<ReviewDTO>>(filteredReviews);
        }

        public async Task<object> GetReviewDashboardAsync()
        {
            var allReviews = await _reviewRepository.GetAllAsync();

            return new
            {
                TotalReviews = allReviews.Count(),
                ApprovedReviews = allReviews.Count(r => r.Status == "Approved"),
                PendingReviews = allReviews.Count(r => r.Status == "Pending"),
                RejectedReviews = allReviews.Count(r => r.Status == "Rejected"),
                AverageRating = allReviews.Any()
                    ? allReviews.Average(r => r.Rating)
                    : 0,
                VerifiedPurchases = allReviews.Count(r => r.IsVerifiedPurchase)
            };
        }
    }
}