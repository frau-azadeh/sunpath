using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models.Dto
{
    public class LiveTelemetryDto
    {
        public int VehicleId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKmh { get; set; }
        public double Heading { get; set; }
        public double? FuelLevelPercent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}