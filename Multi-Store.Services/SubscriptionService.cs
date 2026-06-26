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
            var stores = _context.Stores.ToList();

            foreach (var store in stores)
            {
                // Subscription expired
                if (store.SubscriptionStatus == "Active" &&
                    store.SubscriptionExpiryDate.HasValue &&
                    store.SubscriptionExpiryDate.Value < DateTime.UtcNow)
                {
                    store.SubscriptionStatus = "PaymentDue";

                    if (!store.GracePeriodEndDate.HasValue)
                    {
                        store.GracePeriodEndDate = DateTime.UtcNow.AddDays(7);
                    }
                }

                // Grace period finished
                if (store.GracePeriodEndDate.HasValue &&
                    store.GracePeriodEndDate.Value < DateTime.UtcNow)
                {
                    store.IsSuspended = true;
                    store.SubscriptionStatus = "Suspended";
                }
            }

            _context.SaveChanges();
        }

        // Admin extends subscription
        public void ExtendSubscription(int storeId, string paymentMethod = "Admin")
        {
            var store = _context.Stores.Find(storeId);

            if (store == null)
                throw new Exception("Store not found");
            const decimal amountPaid = 20m;

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
            _context.SubscriptionPayments.Add(new SubscriptionPayment
            {
                StoreId = storeId,
                Amount = amountPaid,
                PaymentDate = DateTime.UtcNow,
                Reference = paymentMethod == "Admin"
    ? "Extended by Admin"
    : "Store Owner Renewal",
                PaymentMethod = paymentMethod
            });

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

        public void ChargeMonthlySubscription()
        {
            var stores = _context.Stores
                .Where(s => s.Status == "Approved")
                .ToList();

            foreach (var store in stores)
            {
                store.OutstandingBalance += 20;

                if (store.SubscriptionExpiryDate.HasValue &&
                    store.SubscriptionExpiryDate.Value <= DateTime.UtcNow)
                {
                    store.SubscriptionStatus = "PaymentDue";
                    store.GracePeriodEndDate = DateTime.UtcNow.AddDays(7);
                }
            }

            _context.SaveChanges();
        }

    }
}