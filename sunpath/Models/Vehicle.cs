using System;

namespace sunpath.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public string PlateNumber { get; set; }
        public string Model { get; set; }
        public int Status { get; set; }

        public decimal? LastLatitude { get; set; }
        public decimal? LastLongitude { get; set; }
        public DateTime? LastUpdateAt { get; set; }

        public double Speed { get; set; }
        public double Heading { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? LastUpdate { get; set; }

        public int VehicleType { get; set; }
        public string InsuranceNumber { get; set; }
        public DateTime? InsuranceExpiryDate { get; set; }
        public int? CurrentDriverId { get; set; }

        // برای نمایش در UI
        public string CurrentDriverName { get; set; }
        public string VehicleTypeName { get; set; }
        public string StatusName { get; set; }
    }
}
