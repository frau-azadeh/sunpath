using System;
namespace sunpath.Models
{
    public class MissionNotification
    {
        public int Id { get; set; }
        public string Audience { get; set; }
        public int? DriverId { get; set; }
        public int MissionId { get; set; }
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }
    }
}
