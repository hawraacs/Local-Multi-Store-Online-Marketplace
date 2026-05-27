
using Multi_Store.Core.Entities;

namespace Multi_Store.Services.Dtos

{
    public class CustomerAddressDTO
    {
        public int AddressID { get; set; }

        public int CustomerID { get; set; }

        public string AddressLine1 { get; set; } = string.Empty;

        public string? AddressLine2 { get; set; }

        public string City { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string? PostalCode { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; }

        // Navigation Properties
        public Customer? Customer { get; set; }
    }
}