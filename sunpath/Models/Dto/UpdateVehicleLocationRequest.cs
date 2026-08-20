using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models.Dto
{
    public class UpdateVehicleLocationRequest
    {
        [Required]
        public int VehicleId { get; set; }

        public int? DriverId { get; set; }

        // در صورت وجود مأموریت فعال، از سمت PWA ارسال می‌شود.
        public int? MissionId { get; set; }

        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Range(-180, 180)]
        public decimal Longitude { get; set; }

        public decimal? Accuracy { get; set; }

        public decimal? Speed { get; set; }

        public decimal? Heading { get; set; }

        // اگر ارسال نشود، سرور UTC Now را ثبت می‌کند.
        public DateTime? RecordedAtUtc { get; set; }
    }
}
