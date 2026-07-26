using System;
using System.Collections.Generic;

namespace Multi_Store.Services.Managers
{
    /// <summary>
    /// Ranks how suitable a delivery person's registered Area is for a
    /// given order's delivery Area, using a simple, hand-maintained
    /// nearby-area lookup.
    ///
    /// This is intentionally kept separate from DeliveryManager and
    /// AdminAssignDeliveryModel so the nearby-area relationships can be
    /// edited in exactly one place later without touching any
    /// assignment, database, or map/location logic.
    ///
    /// It only ever compares the existing Area string values already
    /// used by DeliveryRequest.cshtml and CustomerAddresses.cshtml
    /// (Beirut, Mount Lebanon, Tripoli, Saida, Tyre, Zahle, Bekaa,
    /// Jounieh, Byblos, Nabatieh). No new areas, tables, or database
    /// changes are involved.
    ///
    /// Priority values:
    ///   3 = Same Area
    ///   2 = Nearby Area
    ///   1 = Far Area (default, no relationship found)
    /// </summary>
    public class AreaProximityService
    {
        public const int SameAreaPriority = 3;
        public const int NearbyAreaPriority = 2;
        public const int FarAreaPriority = 1;

        public const string SameAreaLabel = "Same Area";
        public const string NearbyAreaLabel = "Nearby";
        public const string FarAreaLabel = "Far";

        // ==========================================
        // NEARBY-AREA MAP
        //
        // To add or edit nearby relationships later, only this
        // dictionary needs to change. Nothing else in the app depends
        // on its contents.
        // ==========================================
        private static readonly Dictionary<string, string[]> NearbyAreaMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Beirut"] = new[] { "Mount Lebanon", "Jounieh", "Byblos" },
                ["Mount Lebanon"] = new[] { "Beirut", "Jounieh", "Byblos" },
                ["Jounieh"] = new[] { "Beirut", "Mount Lebanon", "Byblos" },
                ["Byblos"] = new[] { "Beirut", "Mount Lebanon", "Jounieh" },

                ["Saida"] = new[] { "Tyre", "Nabatieh" },
                ["Tyre"] = new[] { "Saida", "Nabatieh" },
                ["Nabatieh"] = new[] { "Saida", "Tyre" },

                ["Zahle"] = new[] { "Bekaa" },
                ["Bekaa"] = new[] { "Zahle" },

                // Tripoli has no configured nearby area among the
                // current 10 values, so it always resolves to Far
                // relative to any other area.
                ["Tripoli"] = Array.Empty<string>()
            };

        /// <summary>
        /// Returns 3 (Same Area), 2 (Nearby), or 1 (Far) for the given
        /// customer delivery area and delivery person area. Never
        /// throws - unknown or missing area values simply resolve to
        /// Far so the caller can always safely sort/display a result.
        /// </summary>
        public int GetPriority(string? customerArea, string? deliveryPersonArea)
        {
            if (string.IsNullOrWhiteSpace(customerArea) ||
                string.IsNullOrWhiteSpace(deliveryPersonArea))
            {
                return FarAreaPriority;
            }

            var customer = customerArea.Trim();
            var deliveryPerson = deliveryPersonArea.Trim();

            if (string.Equals(customer, deliveryPerson, StringComparison.OrdinalIgnoreCase))
            {
                return SameAreaPriority;
            }

            if (NearbyAreaMap.TryGetValue(customer, out var nearbyAreas))
            {
                foreach (var nearby in nearbyAreas)
                {
                    if (string.Equals(nearby, deliveryPerson, StringComparison.OrdinalIgnoreCase))
                    {
                        return NearbyAreaPriority;
                    }
                }
            }

            return FarAreaPriority;
        }

        /// <summary>
        /// Returns the display label ("Same Area" / "Nearby" / "Far")
        /// for a given priority value.
        /// </summary>
        public string GetLabel(int priority)
        {
            return priority switch
            {
                SameAreaPriority => SameAreaLabel,
                NearbyAreaPriority => NearbyAreaLabel,
                _ => FarAreaLabel
            };
        }
    }
}
