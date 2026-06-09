using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;
using System.Text.Json;

namespace Local_Multi_Store_Online_Marketplace.Controllers
{
    [ApiController]
    [Route("api/delivery-tracking")]
    [Authorize]
    public class DeliveryTrackingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        private const double AverageDeliverySpeedKmPerHour = 28.0;
        private const int FreshGpsSeconds = 20;

        private static readonly HttpClient RouteHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(7)
        };

        public DeliveryTrackingController(
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("{orderId:int}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetTrackingData(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login first."
                });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserID == user.Id);

            if (customer == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Customer profile was not found."
                });
            }

            var order = await _context.Orders
                .Include(o => o.Address)
                .Include(o => o.DeliveryAssignment)
                    .ThenInclude(a => a.DeliveryPerson)
                .FirstOrDefaultAsync(o =>
                    o.OrderID == orderId &&
                    o.CustomerID == customer.CustomerID);

            if (order == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Order not found."
                });
            }

            if (!IsTrackableStatus(order.Status))
            {
                return Ok(new
                {
                    success = false,
                    message = "Tracking is available only when the order is out for delivery."
                });
            }

            if (order.Address == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Customer address was not found."
                });
            }

            var customerCoordinates = GetCustomerCoordinates(order.Address);

            if (customerCoordinates == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Customer address location is unavailable. Please select the address from the map."
                });
            }

            var assignment = order.DeliveryAssignment;

            if (assignment == null || assignment.DeliveryPerson == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Delivery staff is not assigned yet."
                });
            }

            var deliveryPerson = assignment.DeliveryPerson;

            if (!deliveryPerson.CurrentLatitude.HasValue ||
                !deliveryPerson.CurrentLongitude.HasValue ||
                !deliveryPerson.LastLocationUpdate.HasValue)
            {
                return Ok(new
                {
                    success = false,
                    message = "Delivery staff has not shared GPS yet. Open Delivery Dashboard first."
                });
            }

            var customerLat = customerCoordinates.Value.Latitude;
            var customerLng = customerCoordinates.Value.Longitude;

            var realDeliveryLat = Convert.ToDouble(deliveryPerson.CurrentLatitude.Value);
            var realDeliveryLng = Convert.ToDouble(deliveryPerson.CurrentLongitude.Value);
            var lastGpsUpdateUtc = deliveryPerson.LastLocationUpdate.Value;

            double shownDeliveryLat;
            double shownDeliveryLng;
            string trackingMode;

            if (IsDelivered(order.Status))
            {
                shownDeliveryLat = customerLat;
                shownDeliveryLng = customerLng;
                trackingMode = "Delivered";
            }
            else if (assignment.Status == "OutForDelivery" && assignment.PickupTime.HasValue)
            {
                var secondsSinceLastGps =
                    Math.Max(0, (DateTime.UtcNow - lastGpsUpdateUtc).TotalSeconds);

                var gpsIsFresh = secondsSinceLastGps <= FreshGpsSeconds;

                if (gpsIsFresh)
                {
                    shownDeliveryLat = realDeliveryLat;
                    shownDeliveryLng = realDeliveryLng;
                    trackingMode = "Live GPS";
                }
                else
                {
                    var simulated = await CalculateSimulatedLocationOnRoadAsync(
                        realDeliveryLat,
                        realDeliveryLng,
                        customerLat,
                        customerLng,
                        lastGpsUpdateUtc);

                    shownDeliveryLat = simulated.Latitude;
                    shownDeliveryLng = simulated.Longitude;
                    trackingMode = "Simulated After GPS Stopped";
                }
            }
            else
            {
                shownDeliveryLat = realDeliveryLat;
                shownDeliveryLng = realDeliveryLng;
                trackingMode = "Waiting For Start";
            }

            var remainingRoute = await TryGetDrivingRouteAsync(
                shownDeliveryLat,
                shownDeliveryLng,
                customerLat,
                customerLng);

            var remainingDistanceKm = remainingRoute?.DistanceKm
                ?? CalculateDistanceKm(
                    shownDeliveryLat,
                    shownDeliveryLng,
                    customerLat,
                    customerLng);

            var etaText = IsDelivered(order.Status)
                ? "Arrived"
                : CalculateEtaText(remainingDistanceKm);

            var routeCoordinates = remainingRoute?.Coordinates
                ?? new List<double[]>
                {
                    new[] { shownDeliveryLat, shownDeliveryLng },
                    new[] { customerLat, customerLng }
                };

            return Ok(new
            {
                success = true,

                orderId = order.OrderID,
                orderNumber = order.OrderNumber,
                orderStatus = order.Status,
                assignmentStatus = assignment.Status,

                deliveryLatitude = Math.Round(shownDeliveryLat, 7),
                deliveryLongitude = Math.Round(shownDeliveryLng, 7),

                customerLatitude = Math.Round(customerLat, 7),
                customerLongitude = Math.Round(customerLng, 7),

                routeCoordinates = routeCoordinates,

                lastLocationUpdateText = lastGpsUpdateUtc.ToLocalTime().ToString("HH:mm:ss"),

                trackingMode = trackingMode,
                etaText = etaText,
                distanceKm = Math.Round(remainingDistanceKm, 2),

                message = IsDelivered(order.Status)
                    ? "Your order has arrived."
                    : "Tracking updated successfully."
            });
        }

        [HttpPost("update-location")]
        [Authorize(Roles = "Delivery")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateDeliveryLocationRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid location request."
                });
            }

            if (request.Latitude < -90 ||
                request.Latitude > 90 ||
                request.Longitude < -180 ||
                request.Longitude > 180)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid GPS coordinates."
                });
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Please login first."
                });
            }

            var deliveryPerson = await _context.DeliveryPersons
                .FirstOrDefaultAsync(d =>
                    d.UserID == user.Id &&
                    d.IsActive &&
                    d.Status == "Approved");

            if (deliveryPerson == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Approved delivery profile was not found."
                });
            }

            deliveryPerson.CurrentLatitude = Convert.ToDecimal(request.Latitude);
            deliveryPerson.CurrentLongitude = Convert.ToDecimal(request.Longitude);
            deliveryPerson.LastLocationUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Location updated successfully.",
                latitude = request.Latitude,
                longitude = request.Longitude,
                updatedAt = deliveryPerson.LastLocationUpdate.Value.ToLocalTime().ToString("HH:mm:ss")
            });
        }

        private static bool IsTrackableStatus(string? status)
        {
            return status == "Out for Delivery" ||
                   status == "OutForDelivery" ||
                   status == "Delivered";
        }

        private static bool IsDelivered(string? status)
        {
            return status == "Delivered";
        }

        private static (double Latitude, double Longitude)? GetCustomerCoordinates(CustomerAddress address)
        {
            if (address.Latitude.HasValue && address.Longitude.HasValue)
            {
                return (address.Latitude.Value, address.Longitude.Value);
            }

            var area = address.Area?.Trim().ToLower();

            return area switch
            {
                "beirut" => (33.8938, 35.5018),
                "mount lebanon" => (33.8101, 35.5973),
                "tripoli" => (34.4367, 35.8497),
                "saida" => (33.5631, 35.3689),
                "tyre" => (33.2704, 35.2038),
                "zahle" => (33.8467, 35.9020),
                "bekaa" => (33.8467, 35.9020),
                "jounieh" => (33.9808, 35.6178),
                "byblos" => (34.1230, 35.6519),
                "nabatieh" => (33.3772, 35.4838),
                _ => null
            };
        }

        private static async Task<(double Latitude, double Longitude)> CalculateSimulatedLocationOnRoadAsync(
            double startLat,
            double startLng,
            double endLat,
            double endLng,
            DateTime simulationStartUtc)
        {
            var route = await TryGetDrivingRouteAsync(startLat, startLng, endLat, endLng);

            if (route == null || route.Coordinates.Count < 2 || route.DistanceKm <= 0.05)
            {
                return CalculateSimulatedLocationStraightLine(
                    startLat,
                    startLng,
                    endLat,
                    endLng,
                    simulationStartUtc);
            }

            var elapsedHours = Math.Max(
                0,
                (DateTime.UtcNow - simulationStartUtc).TotalHours);

            var traveledKm = elapsedHours * AverageDeliverySpeedKmPerHour;

            if (traveledKm >= route.DistanceKm)
            {
                return (endLat, endLng);
            }

            return GetPointAlongRoute(route.Coordinates, traveledKm);
        }

        private static (double Latitude, double Longitude) CalculateSimulatedLocationStraightLine(
            double startLat,
            double startLng,
            double endLat,
            double endLng,
            DateTime simulationStartUtc)
        {
            var totalDistanceKm = CalculateDistanceKm(
                startLat,
                startLng,
                endLat,
                endLng);

            if (totalDistanceKm <= 0.05)
            {
                return (endLat, endLng);
            }

            var totalTripMinutes =
                Math.Max(1, (totalDistanceKm / AverageDeliverySpeedKmPerHour) * 60.0);

            var elapsedMinutes =
                Math.Max(0, (DateTime.UtcNow - simulationStartUtc).TotalMinutes);

            var progress = elapsedMinutes / totalTripMinutes;

            if (progress >= 1)
            {
                return (endLat, endLng);
            }

            var currentLat = startLat + ((endLat - startLat) * progress);
            var currentLng = startLng + ((endLng - startLng) * progress);

            return (currentLat, currentLng);
        }

        private static (double Latitude, double Longitude) GetPointAlongRoute(
            List<double[]> routeCoordinates,
            double traveledKm)
        {
            var remainingKm = traveledKm;

            for (var i = 0; i < routeCoordinates.Count - 1; i++)
            {
                var current = routeCoordinates[i];
                var next = routeCoordinates[i + 1];

                var segmentKm = CalculateDistanceKm(
                    current[0],
                    current[1],
                    next[0],
                    next[1]);

                if (segmentKm <= 0)
                {
                    continue;
                }

                if (remainingKm <= segmentKm)
                {
                    var ratio = remainingKm / segmentKm;

                    var lat = current[0] + ((next[0] - current[0]) * ratio);
                    var lng = current[1] + ((next[1] - current[1]) * ratio);

                    return (lat, lng);
                }

                remainingKm -= segmentKm;
            }

            var last = routeCoordinates.Last();

            return (last[0], last[1]);
        }

        private static async Task<RouteResult?> TryGetDrivingRouteAsync(
            double startLat,
            double startLng,
            double endLat,
            double endLng)
        {
            try
            {
                var url =
                    $"https://router.project-osrm.org/route/v1/driving/{startLng},{startLat};{endLng},{endLat}?overview=full&geometries=geojson";

                using var response = await RouteHttpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();

                using var json = await JsonDocument.ParseAsync(stream);

                if (!json.RootElement.TryGetProperty("routes", out var routes))
                {
                    return null;
                }

                if (routes.GetArrayLength() == 0)
                {
                    return null;
                }

                var firstRoute = routes[0];

                var distanceMeters = firstRoute.GetProperty("distance").GetDouble();
                var distanceKm = distanceMeters / 1000.0;

                var coordinates = new List<double[]>();

                var geometryCoordinates = firstRoute
                    .GetProperty("geometry")
                    .GetProperty("coordinates");

                foreach (var point in geometryCoordinates.EnumerateArray())
                {
                    var lng = point[0].GetDouble();
                    var lat = point[1].GetDouble();

                    coordinates.Add(new[] { lat, lng });
                }

                if (coordinates.Count < 2)
                {
                    return null;
                }

                return new RouteResult
                {
                    DistanceKm = distanceKm,
                    Coordinates = coordinates
                };
            }
            catch
            {
                return null;
            }
        }

        private static string CalculateEtaText(double distanceKm)
        {
            if (distanceKm <= 0.15)
            {
                return "Arriving now";
            }

            var estimatedMinutes = Math.Max(
                1,
                (int)Math.Ceiling(distanceKm / AverageDeliverySpeedKmPerHour * 60.0));

            if (estimatedMinutes < 60)
            {
                return $"{estimatedMinutes} min";
            }

            var hours = estimatedMinutes / 60;
            var minutes = estimatedMinutes % 60;

            if (minutes == 0)
            {
                return $"{hours} hr";
            }

            return $"{hours} hr {minutes} min";
        }

        private static double CalculateDistanceKm(
            double lat1,
            double lon1,
            double lat2,
            double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) *
                Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }

    public class UpdateDeliveryLocationRequest
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }
    }

    public class RouteResult
    {
        public double DistanceKm { get; set; }

        public List<double[]> Coordinates { get; set; } = new();
    }
}