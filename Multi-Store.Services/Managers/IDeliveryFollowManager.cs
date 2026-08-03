using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public interface IDeliveryFollowManager
    {
        Task<bool> IsFollowingAsync(int customerId, int deliveryPersonId);

        Task FollowAsync(int customerId, int deliveryPersonId);

        Task UnfollowAsync(int customerId, int deliveryPersonId);

        Task<int> GetFollowersCountAsync(int deliveryPersonId);
    }
}