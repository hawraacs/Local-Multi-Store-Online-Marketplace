using System;
using System.Collections.Generic;

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
    }
}
