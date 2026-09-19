using System;
using System.Collections.Generic;

namespace sunpath.Models.Dto
{
    public class DriverRouteHistoryDto
    {
        public int DispatchId { get; set; }

        public string Title { get; set; }

        public int VehicleId { get; set; }

        public string VehiclePlate { get; set; }

        public string OriginTitle { get; set; }

        public string DestinationTitle { get; set; }

        public decimal? OriginLatitude { get; set; }

        public decimal? OriginLongitude { get; set; }

        public decimal? DestinationLatitude { get; set; }

        public decimal? DestinationLongitude { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public double DistanceKm { get; set; }

        public int DurationSeconds { get; set; }

        public List<DriverRouteHistoryPointDto> Points { get; set; }
            = new List<DriverRouteHistoryPointDto>();
    }

    public class DriverRouteHistoryPointDto
    {
        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public decimal? Accuracy { get; set; }

        public decimal? Speed { get; set; }

        public decimal? Heading { get; set; }

        public DateTime RecordedAtUtc { get; set; }
    }
}