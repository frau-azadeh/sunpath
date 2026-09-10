using System.ComponentModel.DataAnnotations;

namespace sunpath.Models.Dto
{
    public class UpdateDispatchRequest
    {
        [Required] public int VehicleId { get; set; }
        public int? DriverId { get; set; }
        [StringLength(200)] public string Title { get; set; }
        [StringLength(1000)] public string Description { get; set; }
        [StringLength(300)] public string OriginTitle { get; set; }
        public decimal? OriginLatitude { get; set; }
        public decimal? OriginLongitude { get; set; }
        [StringLength(300)] public string DestinationTitle { get; set; }
        public decimal? DestinationLatitude { get; set; }
        public decimal? DestinationLongitude { get; set; }
    }
}