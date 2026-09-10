using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SunPath.Hubs;

namespace SunPath.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MissionDispatchController : ControllerBase
    {
        private readonly IHubContext<FleetHub> _hubContext;

        public MissionDispatchController(IHubContext<FleetHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public class CreateMissionDto
        {
            public int DriverId { get; set; }
            public int VehicleId { get; set; }
            public string OriginName { get; set; } = string.Empty;
            public double OriginLat { get; set; }
            public double OriginLng { get; set; }
            public string DestinationName { get; set; } = string.Empty;
            public double DestinationLat { get; set; }
            public double DestinationLng { get; set; }
            public string Notes { get; set; } = string.Empty;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateMission([FromBody] CreateMissionDto dto)
        {
            if (dto.DriverId <= 0 || dto.DestinationLat == 0 || dto.DestinationLng == 0)
            {
                return BadRequest(new { success = false, message = "اطلاعات مأموریت یا مقصد نامعتبر است." });
            }

            var mission = new
            {
                Id = new Random().Next(100, 9999),
                DriverId = dto.DriverId,
                VehicleId = dto.VehicleId,
                OriginName = string.IsNullOrWhiteSpace(dto.OriginName) ? "مبدأ تعیین‌شده" : dto.OriginName,
                OriginLat = dto.OriginLat,
                OriginLng = dto.OriginLng,
                DestinationName = string.IsNullOrWhiteSpace(dto.DestinationName) ? "مقصد جدید" : dto.DestinationName,
                DestinationLat = dto.DestinationLat,
                DestinationLng = dto.DestinationLng,
                Status = "in_progress",
                AssignedAt = DateTime.UtcNow
            };

            // پوش زنده به راننده اختصاصی از طریق SignalR Hub
            await _hubContext.Clients.All.SendAsync($"MissionAssigned_{dto.DriverId}", mission);
            await _hubContext.Clients.All.SendAsync("NewMissionDispatched", mission);

            return Ok(new
            {
                success = true,
                message = "مأموریت با موفقیت ثبت و برای راننده ارسال شد.",
                mission
            });
        }
    }
}
