using System;
using System.Collections.Generic;
using System.Linq;

namespace Local_Multi_Store_Online_Marketplace.Pages.StoreOwner.Order
{
    // Minimal whitelist for the Store Owner "update order status" entry
    // points only (Index.cshtml.cs and Details.cshtml.cs). This is NOT a
    // project-wide Order.Status enum — it exists purely so these two
    // handlers reject garbage/misspelled values instead of accepting any
    // string. The values below are exactly the Order.Status literals found
    // in use elsewhere in the project (Checkout, OnlinePayment, Admin
    // assignment/cancellation, Delivery pages) as of this fix.
    internal static class AllowedOrderStatuses
    {
        private static readonly HashSet<string> Values =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Pending",
                "Pending Confirmation",
                "Confirmed",
                "Preparing",
                "Ready for Pickup",
                "OutForDelivery",
                "Out for Delivery",
                "Assigned",
                "Delivered",
                "Cancelled"
            };

        public static bool IsValid(string? status) =>
            !string.IsNullOrWhiteSpace(status) && Values.Contains(status.Trim());

        // Fix for H4 (partial, Option 1): terminal statuses are the ones
        // that end an order's lifecycle. Used to block a single store from
        // ending the WHOLE order's status when other stores still have
        // items in it (Order.Status/DeliveryAssignment/Payment are all
        // shared, order-level fields, not per-store).
        private static readonly HashSet<string> TerminalValues =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Delivered",
                "Cancelled"
            };

        public static bool IsTerminal(string? status) =>
            !string.IsNullOrWhiteSpace(status) && TerminalValues.Contains(status.Trim());

        // Fix for multi-store order confirmation. Given one representative
        // OrderItem.StoreResponseStatus per participating store, decides
        // what the shared Order.Status should become right now.
        //
        // Rule: "An order becomes ready for delivery once ALL participating
        // stores have responded (Confirmed or Cancelled), AND at least one
        // store has Confirmed."
        //   - any store still Pending             -> null  (keep waiting)
        //   - all responded, at least one Confirmed -> "Confirmed"
        //   - all responded, all Cancelled          -> "Cancelled"
        //
        // Returns null when the shared Order.Status must NOT change yet —
        // callers should leave it as "Pending" in that case.
        public static string? ComputeOverallStatus(IEnumerable<string> storeResponseStatuses)
        {
            var statuses = storeResponseStatuses.ToList();

            if (statuses.Count == 0)
                return null;

            var anyPending = statuses.Any(s =>
                string.Equals(s, "Pending", StringComparison.OrdinalIgnoreCase));

            if (anyPending)
                return null;

            var anyConfirmed = statuses.Any(s =>
                string.Equals(s, "Confirmed", StringComparison.OrdinalIgnoreCase));

            return anyConfirmed ? "Confirmed" : "Cancelled";
        }
    }
}
