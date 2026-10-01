using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using sunpath.Models;
using sunpath.Services;
using sunpath.Services.Interface;

namespace sunpath.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SimulationController : ControllerBase
    {
        private readonly IDispatchService _dispatches;
        private readonly IVehicleService _vehicles;
        private readonly VehicleSimulationService _simulation;
        public SimulationController(IDispatchService dispatches, IVehicleService vehicles, VehicleSimulationService simulation)
        { _dispatches = dispatches; _vehicles = vehicles; _simulation = simulation; }
        [HttpGet("status/{vehicleId}")]
        public async Task<IActionResult> Status(int vehicleId) => Ok(new { running = await _simulation.IsRunningAsync(vehicleId) });
        [HttpPost("start-mission/{missionId}")]
        public async Task<IActionResult> StartMission(int missionId, [FromBody] SimulationRequest request)
        {
            if (request == null || !Finite(request.SpeedKmh) || request.SpeedKmh < 5 || request.SpeedKmh > 120)
                return BadRequest(new { message = "سرعت شبیه‌سازی باید بین ۵ و ۱۲۰ باشد." });
            var mission = await _dispatches.GetByIdAsync(missionId);
            if (mission == null || await _vehicles.GetByIdAsync(mission.VehicleId) == null) return NotFound(new { message = "مأموریت یا وسیله پیدا نشد." });
            if (mission.Status != DispatchStatus.Assigned && mission.Status != DispatchStatus.Started)
                return BadRequest(new { message = "فقط مأموریت فعال قابل شبیه‌سازی است." });
            if (!mission.OriginLatitude.HasValue || !mission.OriginLongitude.HasValue || !mission.DestinationLatitude.HasValue || !mission.DestinationLongitude.HasValue)
                return BadRequest(new { message = "مبدأ و مقصد را ابتدا ثبت کنید." });
            var origin = new SimulationPoint { Latitude = (double)mission.OriginLatitude.Value, Longitude = (double)mission.OriginLongitude.Value };
            var destination = new SimulationPoint { Latitude = (double)mission.DestinationLatitude.Value, Longitude = (double)mission.DestinationLongitude.Value };
            if (!Valid(origin) || !Valid(destination) || VehicleSimulationService.DistanceKm(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude) < 0.001)
                return BadRequest(new { message = "مختصات مبدأ و مقصد معتبر و متفاوت وارد کنید." });
            var route = request.Route;
            if (route == null || route.Count == 0) route = new List<SimulationPoint> { origin, destination };
            else
            {
                if (route.Count < 2 || route.Count > 10000 || route.Any(point => !Valid(point)) ||
                    VehicleSimulationService.DistanceKm(origin.Latitude, origin.Longitude, route[0].Latitude, route[0].Longitude) > 1 ||
                    VehicleSimulationService.DistanceKm(destination.Latitude, destination.Longitude, route[route.Count - 1].Latitude, route[route.Count - 1].Longitude) > 1)
                    return BadRequest(new { message = "مسیر شبیه‌سازی نامعتبر است." });
                route.Insert(0, origin); route.Add(destination);
            }
            try { await _simulation.StartMissionAsync(mission, request.SpeedKmh, route); }
            catch (InvalidOperationException error) { return BadRequest(new { message = error.Message }); }
            return Ok(new { message = "حرکت آزمایشی از مبدأ به مقصد شروع شد.", vehicleId = mission.VehicleId });
        }
        [HttpDelete("stop/{vehicleId}")]
        public async Task<IActionResult> Stop(int vehicleId)
        { await _simulation.StopAsync(vehicleId); return Ok(new { message = "حرکت آزمایشی متوقف شد." }); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Valid(SimulationPoint point) => point != null && Finite(point.Latitude) && Finite(point.Longitude) && point.Latitude >= -90 && point.Latitude <= 90 && point.Longitude >= -180 && point.Longitude <= 180;
    }
    public class SimulationRequest
    {
        public double SpeedKmh { get; set; } = 40;
        public List<SimulationPoint> Route { get; set; }
    }
}
