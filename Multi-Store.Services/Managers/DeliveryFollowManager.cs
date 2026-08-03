using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using System;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    // =====================================================
    // DELIVERY FOLLOW MANAGER
    //
    // Deliberately kept separate from DeliveryManager, which already
    // owns delivery person registration, approval, assignment, and
    // delivery workflow logic. This manager only owns the follow /
    // unfollow data operation for delivery partners - it does not
    // write notifications or audit logs itself. That mirrors the one
    // confirmed Store precedent we have (StoreCustomerProfileModel),
    // where the PageModel checks IsFollowingAsync first, then calls
    // FollowAsync, and decides on its own whether to notify.
    // =====================================================
    public class DeliveryFollowManager : IDeliveryFollowManager
    {
        private readonly IDeliveryFollowRepository _deliveryFollowRepository;

        public DeliveryFollowManager(
            IDeliveryFollowRepository deliveryFollowRepository)
        {
            _deliveryFollowRepository = deliveryFollowRepository;
        }

        public async Task<bool> IsFollowingAsync(int customerId, int deliveryPersonId)
        {
            return await _deliveryFollowRepository
                .IsFollowingAsync(customerId, deliveryPersonId);
        }

        public async Task FollowAsync(int customerId, int deliveryPersonId)
        {
            // Idempotent - matches the "if (!exists) add" behavior of the
            // existing raw Store follow handlers, so calling this twice
            // for the same pair never creates a duplicate row.
            var existing = await _deliveryFollowRepository
                .GetByCustomerAndDeliveryPersonAsync(customerId, deliveryPersonId);

            if (existing != null)
            {
                return;
            }

            await _deliveryFollowRepository.AddAsync(new DeliveryFollow
            {
                CustomerID = customerId,
                DeliveryPersonID = deliveryPersonId,
                FollowedAt = DateTime.UtcNow
            });
        }

        public async Task UnfollowAsync(int customerId, int deliveryPersonId)
        {
            var existing = await _deliveryFollowRepository
                .GetByCustomerAndDeliveryPersonAsync(customerId, deliveryPersonId);

            if (existing != null)
            {
                await _deliveryFollowRepository.DeleteAsync(existing);
            }
        }

        public async Task<int> GetFollowersCountAsync(int deliveryPersonId)
        {
            return await _deliveryFollowRepository
                .GetFollowersCountAsync(deliveryPersonId);
        }
    }
}