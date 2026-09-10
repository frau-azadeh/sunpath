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
        public string CurrentDriverName { get; set; }
        public string VehicleTypeName { get; set; }
        public string StatusName { get; set; }

        // مأموریت فعال خودرو؛ برای نمایش مسیر در مانیتورینگ.
        public int? ActiveDispatchId { get; set; }
        public int? ActiveDispatchDriverId { get; set; }
        public string OriginAddress { get; set; }
        public decimal? OriginLat { get; set; }
        public decimal? OriginLng { get; set; }
        public string DestinationAddress { get; set; }
        public decimal? DestinationLat { get; set; }
        public decimal? DestinationLng { get; set; }
        public string DispatchStatus { get; set; }
        public double FuelConsumedLiters { get; set; }
        public double TripDistanceKm { get; set; }
        public int TripDurationSeconds { get; set; }
        public int StopDurationSeconds { get; set; }
    }
}