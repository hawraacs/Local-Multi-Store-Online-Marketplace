using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Infrastructure.Data;

namespace Local_Multi_Store_Online_Marketplace.Controllers
{
    [ApiController]
    [Route("api/delivery-tracking")]
    [Authorize]
    public class DeliveryTrackingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        private const double AverageDeliverySpeedKmPerHour = 30.0;

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

            if (order.Status != "Out for Delivery" &&
                order.Status != "OutForDelivery" &&
                order.Status != "Delivered")
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
                    message = "Delivery staff has not shared GPS yet. Open DeliveryDashboard first."
                });
            }

            var customerLat = customerCoordinates.Value.Latitude;
            var customerLng = customerCoordinates.Value.Longitude;

            var realDeliveryLat = Convert.ToDouble(deliveryPerson.CurrentLatitude.Value);
            var realDeliveryLng = Convert.ToDouble(deliveryPerson.CurrentLongitude.Value);

            double shownDeliveryLat;
            double shownDeliveryLng;
            string trackingMode;

            if (order.Status == "Delivered")
            {
                shownDeliveryLat = customerLat;
                shownDeliveryLng = customerLng;
                trackingMode = "Delivered";
            }
            else if (assignment.Status == "OutForDelivery" && assignment.PickupTime.HasValue)
            {
                var simulated = CalculateSimulatedLocation(
                    realDeliveryLat,
                    realDeliveryLng,
                    customerLat,
                    customerLng,
                    assignment.PickupTime.Value);

                shownDeliveryLat = simulated.Latitude;
                shownDeliveryLng = simulated.Longitude;
                trackingMode = "Demo Simulated Movement";
            }
            else
            {
                shownDeliveryLat = realDeliveryLat;
                shownDeliveryLng = realDeliveryLng;
                trackingMode = "Waiting For Start";
            }

            var distanceKm = CalculateDistanceKm(
                shownDeliveryLat,
                shownDeliveryLng,
                customerLat,
                customerLng);

            var etaText = order.Status == "Delivered"
                ? "Arrived"
                : CalculateEtaText(distanceKm);

            return Ok(new
            {
                success = true,

                orderId = order.OrderID,
                orderNumber = order.OrderNumber,
                orderStatus = order.Status,
                assignmentStatus = assignment.Status,

                deliveryLatitude = shownDeliveryLat,
                deliveryLongitude = shownDeliveryLng,

                customerLatitude = customerLat,
                customerLongitude = customerLng,

                lastLocationUpdateText = deliveryPerson.LastLocationUpdate.Value.ToLocalTime().ToString("HH:mm:ss"),

                trackingMode = trackingMode,
                etaText = etaText,
                distanceKm = Math.Round(distanceKm, 2),

                message = order.Status == "Delivered"
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

        private static (double Latitude, double Longitude) CalculateSimulatedLocation(
            double startLat,
            double startLng,
            double endLat,
            double endLng,
            DateTime pickupTimeUtc)
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
                Math.Max(0, (DateTime.UtcNow - pickupTimeUtc).TotalMinutes);

            var progress = elapsedMinutes / totalTripMinutes;

            if (progress >= 1)
            {
                return (endLat, endLng);
            }

            var currentLat = startLat + ((endLat - startLat) * progress);
            var currentLng = startLng + ((endLng - startLng) * progress);

            return (currentLat, currentLng);
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

            return $"{estimatedMinutes} min";
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
}