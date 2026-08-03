using Multi_Store.Services.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public interface IDeliveryReviewManager
    {
        Task<int> AddReviewAsync(
            DeliveryReviewDTO reviewDTO,
            string ipAddress,
            string userAgent);

        Task<IEnumerable<DeliveryReviewDTO>> GetReviewsByDeliveryPersonAsync(int deliveryPersonId);

        Task<IEnumerable<DeliveryReviewDTO>> GetReviewsByCustomerAsync(int customerId);

        Task<bool> ExistsForAssignmentAsync(int assignmentId);

        Task<double> GetAverageDeliveryPersonRatingAsync(int deliveryPersonId);

        Task<int> GetTotalReviewsCountAsync(int deliveryPersonId);
    }
}