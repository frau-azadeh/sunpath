using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models
{
    public class VehicleLocationHistory
    {
        public long Id { get; set; }

        public int VehicleId { get; set; }

        public int? DriverId { get; set; }

        public int? MissionId { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public decimal? Accuracy { get; set; }

        // سرعت بر حسب متر بر ثانیه؛ مقدار Geolocation API مرورگر
        public decimal? Speed { get; set; }

        // جهت حرکت بین 0 تا 360 درجه
        public decimal? Heading { get; set; }

        public DateTime RecordedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
