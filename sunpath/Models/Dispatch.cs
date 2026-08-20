using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models
{
    public class Dispatch
    {
        public int Id { get; set; }

        public int? DriverId { get; set; }

        public int VehicleId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string OriginTitle { get; set; }

        public decimal? OriginLatitude { get; set; }

        public decimal? OriginLongitude { get; set; }

        public string DestinationTitle { get; set; }

        public decimal? DestinationLatitude { get; set; }

        public decimal? DestinationLongitude { get; set; }

        public DispatchStatus Status { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }
    }
}