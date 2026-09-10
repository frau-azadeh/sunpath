using System;
using System.ComponentModel.DataAnnotations;

namespace sunpath.Models.Dto
{
    public class UpdateVehicleLocationRequest
    {
        [Required] public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        public int? MissionId { get; set; }
        [Range(-90, 90)] public decimal Latitude { get; set; }
        [Range(-180, 180)] public decimal Longitude { get; set; }
        public decimal? Accuracy { get; set; }
        public decimal? Speed { get; set; }
        public decimal? Heading { get; set; }
        public DateTime? RecordedAtUtc { get; set; }
    }
}