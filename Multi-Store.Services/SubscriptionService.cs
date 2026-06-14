using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Multi_Store.Services
{
    public class SubscriptionService
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Call this when a store is approved – start 30-day free trial
        public void StartFreeTrial(Store store)
        {
            store.TrialStartDate = DateTime.UtcNow;
            store.SubscriptionExpiryDate = DateTime.UtcNow.AddMonths(1);
            store.SubscriptionStatus = "Active";

            _context.SaveChanges();
        }

        // DAILY JOB (called by BackgroundService)
        public void UpdateExpiredStores()
        {
            var expiredStores = _context.Stores
                .Where(s =>
                    s.SubscriptionStatus == "Active" &&
                    s.SubscriptionExpiryDate != null &&
                    s.SubscriptionExpiryDate < DateTime.UtcNow)
                .ToList();

            foreach (var store in expiredStores)
            {
                store.SubscriptionStatus = "Expired";
            }

            _context.SaveChanges();
        }

        // Admin extends subscription
        public void ExtendSubscription(int storeId, decimal amountPaid, string paymentMethod = "Admin")
        {
            var store = _context.Stores.Find(storeId);

            if (store == null)
                throw new Exception("Store not found");

            DateTime newExpiry;

            if (store.SubscriptionExpiryDate == null ||
                store.SubscriptionExpiryDate < DateTime.UtcNow)
            {
                newExpiry = DateTime.UtcNow.AddMonths(1); ;
            }
            else
            {
                newExpiry = store.SubscriptionExpiryDate.Value.AddMonths(1);
            }

            store.SubscriptionExpiryDate = newExpiry;
            store.SubscriptionStatus = "Active";
            store.LastPaymentDate = DateTime.UtcNow;
            store.LastPaymentAmount = amountPaid;

            _context.SaveChanges();
        }

        // Suspend / Activate store
        public void SetStoreStatus(int storeId, string status)
        {
            var store = _context.Stores.Find(storeId);

            if (store == null)
                return;

            if (status == "Active" &&
                store.SubscriptionExpiryDate < DateTime.UtcNow)
            {
                throw new Exception("Cannot activate an expired store without extending subscription.");
            }

            store.SubscriptionStatus = status;
            _context.SaveChanges();
        }

        // Can manage products?
        public bool CanManageProducts(int storeId)
        {
            var store = _context.Stores
                .AsNoTracking()
                .FirstOrDefault(s => s.StoreID == storeId);

            return store != null && store.SubscriptionStatus == "Active";
        }

        // Can receive orders?
        public bool CanReceiveOrders(int storeId)
        {
            var store = _context.Stores
                .AsNoTracking()
                .FirstOrDefault(s => s.StoreID == storeId);

            if (store == null)
                return false;

            if (store.SubscriptionStatus != "Active")
                return false;

            if (store.SubscriptionExpiryDate.HasValue &&
                store.SubscriptionExpiryDate.Value < DateTime.UtcNow)
                return false;

            return true;
        }
    }
}