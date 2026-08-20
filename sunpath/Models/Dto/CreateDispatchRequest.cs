using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models.Dto
{
    public class CreateDispatchRequest
    {
        [Required]
        public int VehicleId { get; set; }

        public int? DriverId { get; set; }

        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(300)]
        public string OriginTitle { get; set; }

        public decimal? OriginLatitude { get; set; }

        public decimal? OriginLongitude { get; set; }

        [StringLength(300)]
        public string DestinationTitle { get; set; }

        public decimal? DestinationLatitude { get; set; }

        public decimal? DestinationLongitude { get; set; }
    }
}