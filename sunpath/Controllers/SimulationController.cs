using Microsoft.AspNetCore.Mvc;
using System;
using sunpath.Services;
using sunpath.Services.Interface;
using System.Threading.Tasks;

namespace sunpath.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SimulationController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly VehicleSimulationService _simulationService;

        public SimulationController(
            IVehicleService vehicleService,
            VehicleSimulationService simulationService)
        {
            _vehicleService = vehicleService;
            _simulationService = simulationService;
        }

        // شروع حرکت خودکار
        // GET/POST: /api/simulation/start/{id}?lat=35.70000&lng=51.35000
        [HttpPost("start/{id}")]
        public async Task<IActionResult> Start(int id, [FromQuery] double lat, [FromQuery] double lng)
        {
            _simulationService.Start(id, lat, lng);
            return Ok(new { message = $"شبیه‌سازی خودرو {id} شروع شد", vehicleId = id });
        }

        // اگر نمیدونی مختصات دقیق خودرو چیه، این متد از خود DB خونده و شروع می‌کنه
        [HttpPost("start-from-db/{id}")]
        public async Task<IActionResult> StartFromDb(int id)
        {
            var vehicles = await _vehicleService.GetAllVehiclesAsync();
            foreach (var vehicle in vehicles)
            {
                if (vehicle.Id == id)
                {
                    _simulationService.Start(id, vehicle.Latitude, vehicle.Longitude, vehicle.Heading);
                    return Ok(new { message = $"شبیه‌سازی از موقعیت فعلی DB شروع شد", vehicleId = id });
                }
            }
            return NotFound(new { message = "خودرو پیدا نشد" });
        }

        // توقف حرکت خودکار
        // DELETE: /api/simulation/stop/{id}
        [HttpDelete("stop/{id}")]
        public IActionResult Stop(int id)
        {
            _simulationService.Stop(id);
            return Ok(new { message = $"شبیه‌سازی خودرو {id} متوقف شد", vehicleId = id });
        }

        // حرکت دستی (یک پله فقط) — بدون شبیه‌سازی، مستقیم از فرانت صدا بزن
        [HttpPost("move/{id}")]
        public async Task<IActionResult> Move(int id, [FromBody] VehiclePositionPayload payload)
        {
            await _vehicleService.UpdateVehicleStatusAsync(
                id,
                payload.Latitude,
                payload.Longitude,
                payload.Speed,
                payload.Heading);
            return Ok(new { message = "موقعیت دستی ارسال شد" });
        }
    }

    // مدل داخلی برای حرکت دستی — هماهنگ با فرانت
    public class VehiclePositionPayload
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double Heading { get; set; }
    }
}
