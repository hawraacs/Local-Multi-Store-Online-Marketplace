namespace Multi_Store.Services.Dtos
{
    public class DeliveryAreaDTO
    {
        public int DeliveryAreaID { get; set; }
        public int StoreID { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string BoundaryType { get; set; } = string.Empty;
        public decimal? RadiusKm { get; set; }
        public string? PolygonCoordinates { get; set; }
        public decimal BaseDeliveryFee { get; set; }
        public decimal FeePerKm { get; set; }
        public decimal? FreeDeliveryThreshold { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual Store Store { get; set; } = null!;
    }
}